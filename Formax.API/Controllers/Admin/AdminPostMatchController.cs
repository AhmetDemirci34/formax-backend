using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Application.Services.PostMatch;
using Formax.Infrastructure.BackgroundJobs;
using Microsoft.AspNetCore.Mvc;

namespace Formax.API.Controllers.Admin
{
    /// <summary>
    /// MAÇ SONRASI VİDEO — operasyon/teşhis uçları.
    ///
    /// Kayıt ucu bir KISA YOL DEĞİLDİR: elle verilen aday da arka plan turunun geçtiği
    /// kimlik ve embed kapısından (<see cref="IMatchVideoRegistrar"/>) geçer. Yani
    /// buradan yanlış ayağın, resmî olmayan bir kaynağın ya da gömmeye kapalı bir
    /// videonun "oynatılabilir" olarak girmesi mümkün değildir.
    /// </summary>
    [ApiController]
    [Route("admin/post-match")]
    public class AdminPostMatchController : ControllerBase
    {
        private readonly IMatchVideoRegistrar _registrar;
        private readonly IMatchVideoReader _reader;
        private readonly PostMatchEnrichmentJob _job;

        public AdminPostMatchController(
            IMatchVideoRegistrar registrar, IMatchVideoReader reader, PostMatchEnrichmentJob job)
        {
            _registrar = registrar; _reader = reader; _job = job;
        }

        /// <summary>
        /// SAĞLAYICI ZİNCİRİNİN DURUMU — SALT OKUNUR, sıfır dış istek.
        ///
        /// "Neden bu maça video bulunamadı?" sorusunun ilk durağı: hangi resmî kaynak
        /// yapılandırılmış, hangisi <c>NotConfigured</c> diye atlanıyor. Bu uç hiçbir
        /// sağlayıcıyı ÇAĞIRMAZ; yalnız yapılandırmayı okur.
        /// </summary>
        [HttpGet("video/providers")]
        public IActionResult Providers(
            [FromServices] Formax.Infrastructure.PostMatch.OfficialSiteFeedVideoProvider siteFeeds,
            [FromServices] Formax.Infrastructure.PostMatch.YouTubeDataApiVideoProvider dataApi,
            [FromServices] Formax.Infrastructure.PostMatch.YouTubeChannelFeedVideoProvider channelFeeds)
        {
            var members = new IOfficialMatchVideoProvider[] { siteFeeds, dataApi, channelFeeds };

            return Ok(new
            {
                chain = members
                    .OrderBy(p => p.Priority)
                    .Select(p => new
                    {
                        provider = p.Name,
                        priority = p.Priority,
                        tierLabel = OfficialVideoSourceTiers.Label(p.Priority),
                        status = p.Status
                    }),
                // İzin listesi: bir videonun "resmî" sayılmasının tek ölçütü.
                allowedSources = OfficialVideoSources.All.Select(s => new
                {
                    s.Key, s.Publisher, s.Platform,
                    // "tier" ve "Tier" camelCase serilestirmede AYNI ada duser —
                    // ikisini birden yazmak calisma zamaninda JSON catismasi uretir.
                    tierValue = s.Tier,
                    tierLabel = OfficialVideoSourceTiers.Label(s.Tier),
                    s.AllowsInAppEmbed
                })
            });
        }

        /// <summary>
        /// VİDEO İSTEK VE KARAR DÖKÜMÜ — SALT OKUNUR. API-Football sayacından tamamen ayrıdır.
        /// Sorgu dizesi kayda hiç girmez (Data API anahtarı sorguda taşınır).
        /// </summary>
        [HttpGet("video/requests")]
        public IActionResult VideoRequests([FromServices] Formax.Infrastructure.Telemetry.VideoDiscoveryRequestLog log)
        {
            var requests = log.RequestSnapshot();
            var verdicts = log.VerdictSnapshot();
            return Ok(new
            {
                totalRequestsSinceStart = log.TotalRequests,
                // "cache" satırı gerçek HTTP DEĞİLDİR; host sayımı yalnız ağa çıkanları sayar.
                byProviderAndHost = requests
                    .Where(r => r.Result != "cache")
                    .GroupBy(r => r.Provider + "|" + r.Host)
                    .ToDictionary(g => g.Key, g => g.Count()),
                cacheServed = requests.Count(r => r.Result == "cache"),
                requests,
                verdicts
            });
        }

        /// <summary>
        /// KONTROLLÜ KEŞİF — tek maç için zinciri ŞİMDİ çalıştırır ve her adayın kararını
        /// döker. Aday yine kimlik + embed kapısından (<see cref="IMatchVideoRegistrar"/>)
        /// geçer: kabul edilirse kaydedilir, edilmezse gerekçesiyle reddedilir.
        ///
        /// KALICI DEFTERE DOKUNMAZ: bu elle tetiklenen bir teşhistir, arka plan işinin
        /// deneme hakkını harcamaz.
        /// </summary>
        [HttpPost("video/{matchId:int}/discover")]
        public async Task<IActionResult> Discover(
            int matchId,
            [FromServices] IOfficialMatchVideoProvider provider,
            [FromServices] Formax.Infrastructure.Telemetry.VideoDiscoveryRequestLog log,
            CancellationToken ct)
        {
            var identity = await _registrar.BuildIdentityAsync(matchId, ct);
            if (identity == null) return NotFound(new { matchId, error = "mac veya fikstur kimligi yok" });

            var before = log.TotalRequests;
            var found = await provider.DiscoverAsync(identity, ct);

            var verdicts = new List<object>();
            foreach (var c in found)
            {
                var r = await _registrar.RegisterAsync(matchId, c, ct);
                log.RecordVerdict(new Formax.Infrastructure.Telemetry.VideoDiscoveryRequestLog.VerdictEntry(
                    DateTime.UtcNow, c.ProviderName, matchId, identity.ExternalFixtureId,
                    c.SourceIdentifier, c.ExternalVideoId, c.Title, r.Stored, r.Status, r.Reason));
                verdicts.Add(new
                {
                    provider = c.ProviderName,
                    source = c.SourceIdentifier,
                    c.ExternalVideoId,
                    c.Title,
                    c.PublishedUtc,
                    c.SourcePageUrl,
                    accepted = r.Stored,
                    r.Status,
                    r.Reason
                });
            }

            var diag = provider as IVideoDiscoveryDiagnostics;
            return Ok(new
            {
                matchId,
                identity.ExternalFixtureId,
                home = identity.HomeTeamName,
                away = identity.AwayTeamName,
                kickoffUtc = identity.MatchDateUtc,
                chainCompleted = diag?.LastRunCompleted,
                providers = diag?.LastOutcomes,
                requestsMade = log.TotalRequests - before,
                candidates = found.Count,
                verdicts
            });
        }

        /// <summary>Bir tur çalıştırır (bitmiş + kilitli kapsam + tekrar takvimi).</summary>
        [HttpPost("video/run")]
        public async Task<IActionResult> Run(CancellationToken ct)
            => Ok(new { processed = await _job.RunCycleAsync(ct) });

        /// <summary>
        /// GERİYE DÖNÜK DENETİM. Varsayılan SALT OKUNURDUR: <c>?apply=true</c> verilmedikçe
        /// hiçbir satır değişmez. Uygulandığında yalnız KANITLANMIŞ yanlış kayıtlar
        /// <c>Rejected</c>, kanıtı yetersizler <c>NeedsManualReview</c> olur; hiçbir satır
        /// SİLİNMEZ ve doğrulanmış doğru kayıtlara dokunulmaz.
        /// </summary>
        [HttpPost("video/audit")]
        public async Task<IActionResult> Audit(
            [FromServices] Formax.Infrastructure.PostMatch.MatchVideoAuditService audit,
            [FromQuery] bool apply = false,
            CancellationToken ct = default)
            => Ok(new { applied = apply, report = await audit.RunAsync(apply, ct) });

        /// <summary>Maçın kayıtlı videoları — ekranın gördüğü şeyin aynısı.</summary>
        [HttpGet("video/{matchId:int}")]
        public IActionResult List(int matchId)
        {
            var videos = _reader.GetVideos(matchId);
            return Ok(new
            {
                matchId,
                count = videos.Count,
                playable = videos.Count(v => v.CanPlayInApp),
                videos
            });
        }

        /// <summary>
        /// DOĞRULANMIŞ ADAYI KAYDET. Reddedilirse KÖK NEDEN döner — sessizce yutulmaz.
        /// </summary>
        [HttpPost("video/{matchId:int}")]
        public async Task<IActionResult> Register(
            int matchId, [FromBody] RegisterVideoRequest body, CancellationToken ct)
        {
            if (body == null || string.IsNullOrWhiteSpace(body.ExternalVideoId))
                return BadRequest(new { error = "externalVideoId zorunlu" });

            var candidate = new OfficialVideoCandidate(
                Platform: string.IsNullOrWhiteSpace(body.Platform) ? "YouTube" : body.Platform!,
                SourceIdentifier: body.SourceIdentifier ?? string.Empty,
                ExternalVideoId: body.ExternalVideoId!,
                Title: body.Title ?? string.Empty,
                Description: body.Description,
                PublishedUtc: body.PublishedUtc,
                SourcePageUrl: body.SourcePageUrl ?? string.Empty,
                ThumbnailUrl: body.ThumbnailUrl,
                DurationSeconds: body.DurationSeconds,
                AvailableCountries: body.AvailableCountries,
                EventMinute: body.EventMinute,
                EventExtraMinute: body.EventExtraMinute,
                EventPlayer: body.EventPlayer,
                EventTeam: body.EventTeam);

            var result = await _registrar.RegisterAsync(matchId, candidate, ct);
            return result.Stored
                ? Ok(result)
                : StatusCode(result.Status == "Duplicate" ? 200 : 422, result);
        }

        public sealed class RegisterVideoRequest
        {
            public string? Platform { get; set; }
            /// <summary>YouTube kanal kimliği (UC…) ya da web kaynağı anahtarı.</summary>
            public string? SourceIdentifier { get; set; }
            public string? ExternalVideoId { get; set; }
            public string? Title { get; set; }
            public string? Description { get; set; }
            public DateTime PublishedUtc { get; set; }
            public string? SourcePageUrl { get; set; }
            public string? ThumbnailUrl { get; set; }
            public int? DurationSeconds { get; set; }

            /// <summary>
            /// Kaynağın bildirdiği ülke listesi (ISO alpha-2). Boş bırakılırsa kısıt
            /// "yok" değil BİLİNMİYOR sayılır — "her yerde açık" varsayılmaz.
            /// </summary>
            public List<string>? AvailableCountries { get; set; }

            // Olay klibi meta verisi — yalnız AYRI gol/önemli an klibinde doldurulur.
            public int? EventMinute { get; set; }
            public int? EventExtraMinute { get; set; }
            public string? EventPlayer { get; set; }
            public string? EventTeam { get; set; }
        }
    }
}
