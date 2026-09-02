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

        /// <summary>Bir tur çalıştırır (bitmiş + kilitli kapsam + tekrar takvimi).</summary>
        [HttpPost("video/run")]
        public async Task<IActionResult> Run(CancellationToken ct)
            => Ok(new { processed = await _job.RunCycleAsync(ct) });

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
