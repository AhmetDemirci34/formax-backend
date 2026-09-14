using System;
using System.Threading;
using System.Threading.Tasks;
using Formax.API.Common;
using Formax.Application.Interfaces;
using Formax.Application.UseCases;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Formax.API.Controllers
{
    [ApiController]
    [Route("api/matches")]
    public class MatchController : ControllerBase
    {
        private readonly GetMatchDetailAIContextUseCase _useCase;
        private readonly GetMatchSocialPostsUseCase _socialUseCase;
        private readonly GetMatchAiSignalsUseCase _aiSignalsUseCase;
        private readonly GetMatchDecisionUseCase _decisionUseCase;
        private readonly GetMatchVoiceUseCase _voiceUseCase;
        private readonly GetMatchNarrativeUseCase _narrativeUseCase;
        private readonly GetMatchNewsUseCase _newsUseCase;
        private readonly GetMatchLiveFeedUseCase _liveFeedUseCase;
        private readonly GetMatchHighlightsUseCase _highlightsUseCase;
        private readonly ILogger<MatchController> _logger;

        public MatchController(
            GetMatchDetailAIContextUseCase useCase,
            GetMatchSocialPostsUseCase socialUseCase,
            GetMatchAiSignalsUseCase aiSignalsUseCase,
            GetMatchDecisionUseCase decisionUseCase,
            GetMatchVoiceUseCase voiceUseCase,
            GetMatchNarrativeUseCase narrativeUseCase,
            GetMatchNewsUseCase newsUseCase,
            GetMatchLiveFeedUseCase liveFeedUseCase,
            GetMatchHighlightsUseCase highlightsUseCase,
            ILogger<MatchController> logger)
        {
            _liveFeedUseCase = liveFeedUseCase;
            _highlightsUseCase = highlightsUseCase;
            _useCase = useCase;
            _socialUseCase = socialUseCase;
            _aiSignalsUseCase = aiSignalsUseCase;
            _decisionUseCase = decisionUseCase;
            _voiceUseCase = voiceUseCase;
            _narrativeUseCase = narrativeUseCase;
            _newsUseCase = newsUseCase;
            _logger  = logger;
        }

        // =========================
        // 🔬 GDP AI SIGNAL FACTORY — DIAGNOSTIC (üretilen AI Signal listesi; doğrulama)
        // =========================
        [HttpGet("{matchId}/ai-signals")]
        public IActionResult GetMatchAiSignals(int matchId)
        {
            var result = _aiSignalsUseCase.Execute(matchId);
            if (result == null) return NotFound(new { error = "Match not found", matchId });
            return Ok(result);
        }

        // =========================
        // 🧠 FORMAX BEYNİ — MarketProbabilityEngine AI DECISION PACKAGE (diagnostic; doğrulama)
        // =========================
        [HttpGet("{matchId}/decision")]
        public async Task<IActionResult> GetMatchDecision(int matchId, CancellationToken ct)
        {
            var result = await _decisionUseCase.ExecuteAsync(matchId, ct);
            if (result == null) return NotFound(new { error = "Match not found", matchId });
            return Ok(result);
        }

        // =========================
        // 📝 FOOTBALL INTELLIGENCE v2 — LLM NARRATIVE (FootballIntelligence → doğal Türkçe yorum)
        // ?surface=DiscoverCard|MatchDetail|AiIncele
        // =========================
        [HttpGet("{matchId}/narrative")]
        public IActionResult GetMatchNarrative(int matchId, [FromQuery] string surface = "AiIncele")
        {
            var parsed = surface?.ToLowerInvariant() switch
            {
                "discovercard" or "discover" => Formax.Application.AI.LLM.FootballNarrativeSurface.DiscoverCard,
                "matchdetail" or "detail"    => Formax.Application.AI.LLM.FootballNarrativeSurface.MatchDetail,
                _                             => Formax.Application.AI.LLM.FootballNarrativeSurface.AiIncele
            };
            var result = _narrativeUseCase.Execute(matchId, parsed);
            if (result == null) return NotFound(new { error = "Match not found", matchId });
            return Ok(result);
        }

        // =========================
        // 🗣️ FORMAX'IN SESİ — Voice Composer (AiDecisionPackage → system+user prompt; diagnostic)
        // ?screen=Discover|MatchDetail|Live|Radar|Notification|Global
        // =========================
        [HttpGet("{matchId}/voice")]
        public IActionResult GetMatchVoice(int matchId, [FromQuery] string? screen = null)
        {
            var result = _voiceUseCase.Execute(matchId, screen);
            if (result == null) return NotFound(new { error = "Match not found", matchId });
            return Ok(result);
        }

        // =========================
        // ✅ MATCH DETAIL > Flash Gelişmeler / Resmi Paylaşımlar (Canonical Social)
        // =========================
        [HttpGet("{matchId}/social")]
        public IActionResult GetMatchSocial(int matchId)
        {
            var posts = _socialUseCase.Execute(matchId);
            return Ok(posts);
        }

        // =========================
        // ✅ MATCH DETAIL > SON DAKİKA (dile duyarlı haber listesi)
        // =========================
        /// <summary>
        /// Maçın Son Dakika haberleri, kullanıcının FORMAX dilinde.
        ///
        /// Liste <c>/detail</c> ile AYNI servisten üretilir (MatchNewsFeedService) — tek fark
        /// çeviridir. Çeviri backend'in mevcut LLM zinciriyle yapılır ve (ContentHash, Language)
        /// ile kalıcı önbelleğe alınır; aynı haber aynı dilde ikinci kez çevrilmez.
        ///
        /// Desteklenmeyen/boş <paramref name="lang"/> → içerik ORİJİNAL diliyle döner.
        /// </summary>
        [HttpGet("{matchId}/news")]
        public async Task<IActionResult> GetMatchNews(
            int matchId, [FromQuery] string? lang, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _newsUseCase.ExecuteAsync(matchId, lang, cancellationToken);
                if (result == null)
                    return NotFound(new { error = "Match not found", matchId });

                return Ok(result);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Hata "haber yok" diye MASKELENMEZ — istemci boş durum ile hatayı ayırır.
                _logger.LogError(ex, "GetMatchNews failed for matchId={MatchId} lang={Lang}", matchId, lang);
                return StatusCode(500, new { error = "News unavailable", matchId });
            }
        }

        // =========================
        // ✅ MATCH DETAIL > CANLI TAKİP (global kaynaklı canlı akış)
        // =========================
        /// <summary>
        /// Maçın canlı takip akışı: durum + (varsa) canlı skor/dakika + ters kronolojik
        /// gelişmeler (en yeni en üstte).
        ///
        /// AI MAÇ ANALİZİ BU UÇTA ÇALIŞMAZ — Decision/Narrative zinciri çağrılmaz.
        /// Anlatı global kaynaklardan (haber deposu + resmi sosyal paylaşım) gelir,
        /// api-football canlı olayları bu akışın kaynağı değildir.
        /// </summary>
        [HttpGet("{matchId}/livefeed")]
        public async Task<IActionResult> GetMatchLiveFeed(int matchId, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _liveFeedUseCase.ExecuteAsync(matchId, cancellationToken);
                if (result == null)
                    return NotFound(new { error = "Match not found", matchId });

                return Ok(result);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Hata "gelişme yok" diye MASKELENMEZ.
                _logger.LogError(ex, "GetMatchLiveFeed failed for matchId={MatchId}", matchId);
                return StatusCode(500, new { error = "Live feed unavailable", matchId });
            }
        }

        // =========================
        // ✅ MATCH DETAIL > ÖNEMLİ ANLAR
        // =========================
        /// <summary>
        /// Maçın önemli anları: gerçek maç olayları (gol/penaltı/kart/VAR) ve maça KATI
        /// şekilde eşleşen, platformun embed'ine izin verdiği video içerikleri.
        /// Video indirilmez/yeniden yayınlanmaz. İçerik yoksa Status = "NoContent".
        /// </summary>
        [HttpGet("{matchId}/highlights")]
        public IActionResult GetMatchHighlights(int matchId)
        {
            try
            {
                var result = _highlightsUseCase.Execute(matchId);
                if (result == null)
                    return NotFound(new { error = "Match not found", matchId });

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetMatchHighlights failed for matchId={MatchId}", matchId);
                return StatusCode(500, new { error = "Highlights unavailable", matchId });
            }
        }

        // =========================
        // ✅ MATCH DETAIL (TEK ENDPOINT)
        // =========================
        [HttpGet("{matchId}/detail")]
        public async Task<IActionResult> GetMatchDetail(int matchId, CancellationToken cancellationToken)
        {
            try
            {
                var handler = System.Diagnostics.Stopwatch.StartNew();
                var result = await _useCase.ExecuteAsync(matchId, cancellationToken);
                HttpContext.Items[Formax.API.Middleware.DetailTimingMiddleware.HandlerMsKey] = handler.ElapsedMilliseconds;

                if (result == null)
                    return NotFound(new { error = "Match not found", matchId });

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetMatchDetail failed for matchId={MatchId}", matchId);

                return StatusCode(500, new
                {
                    error   = "Match detail could not be loaded",
                    message = ex.Message,
                    inner   = ex.InnerException?.Message,
                    type    = ex.GetType().Name,
                    matchId
                });
            }
        }

        // =========================
        // ✅ FEED
        // =========================
        /// <summary>
        /// Maç listesi — GERÇEK veri. Eskiden bu aksiyon iki adet SABİT/UYDURMA maç
        /// (Galatasaray-Fenerbahçe, Barcelona-Atletico) döndürüyordu; Maçlar ekranı
        /// bu yüzden mock ile çalışıyordu. Artık zaten var olan GetMatchesUseCase
        /// kullanılır (yeni uç/servis/mantık YOK — yalnız mock kaldırıldı).
        /// Status (Scheduled/Live/Finished), skor ve dakika gerçek veridir.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetMatches([FromServices] GetMatchesUseCase useCase)
        {
            var userId = User.GetUserId();
            return Ok(await useCase.Handle(userId));
        }

        // =========================
        // 📅 SONUÇLAR — "Maçlar → SONUÇLAR" sekmesi
        // =========================
        //
        // NEDEN AYRI UÇ: yukarıdaki GET /api/matches "şimdi" merkezli YAKLAŞAN listesidir.
        // Durumu SAATTEN türetir, biten maçlar için yalnız 8 saatlik bir kuyruk taşır ve
        // her çağrıda Sapma Motoru + kullanıcı ilgi sıralaması çalıştırır. Sonuç listesi
        // bunların hiçbirine ihtiyaç duymaz, buna karşılık gün bazlı okumaya ve depodaki
        // GERÇEK duruma ihtiyaç duyar. Mevcut sözleşme bozulmadan additive olarak eklendi.
        //
        // Her iki uç da SALT DB'dir: bu sayfayı açmak api-football'a istek ÜRETMEZ.

        /// <summary>
        /// Bir Türkiye takvim gününün bitmiş maçları (kilitli 11 organizasyon).
        /// <paramref name="date"/> "yyyy-MM-dd" biçiminde ve GELECEK olamaz.
        /// </summary>
        [HttpGet("results")]
        public async Task<IActionResult> GetResults(
            [FromServices] IMatchResultsReader reader,
            [FromQuery] string? date,
            CancellationToken ct)
        {
            var today = Formax.Infrastructure.Time.IstanbulCalendar.TodayIn(DateTime.UtcNow);

            DateOnly day;
            if (string.IsNullOrWhiteSpace(date)) day = today;
            else if (!DateOnly.TryParseExact(date, "yyyy-MM-dd",
                         System.Globalization.CultureInfo.InvariantCulture,
                         System.Globalization.DateTimeStyles.None, out day))
                return BadRequest(new { error = "date 'yyyy-MM-dd' biçiminde olmalı" });

            // GELECEĞE BAKILMAZ: oynanmamış maçın sonucu yoktur.
            if (day > today)
                return BadRequest(new { error = "gelecek bir gün için sonuç istenemez" });

            var results = await reader.GetResultsAsync(day, ct);
            return Ok(new { date = day.ToString("yyyy-MM-dd"), count = results.Count, results });
        }

        /// <summary>
        /// Son <paramref name="days"/> Türkiye günü içinde SONUÇ BULUNAN günler.
        /// Tarih seçici "en yakın sonuçlu gün"ü buradan seçer — 8 gün için 8 istek atmaz.
        /// </summary>
        /// <summary>
        /// TAKIM ARAMASI — "Takım ara…" kutusunun ucu.
        ///
        /// <paramref name="scope"/>: <c>upcoming</c> → başlamamış maçlar (mevcut sezonun
        /// GELECEĞİ), <c>finished</c> → mevcut sezonun bitmiş maçları. Her iki hâlde de
        /// kapsam kilitli 11 organizasyondur ve arama SEÇİLİ GÜNLE SINIRLI DEĞİLDİR.
        ///
        /// Salt DB okur: aramak api-football kotası harcamaz, video keşfi ya da LLM
        /// tetiklemez. İki karakterden kısa terim DB'ye hiç gitmez.
        /// </summary>
        [HttpGet("search")]
        public async Task<IActionResult> SearchByTeam(
            [FromServices] IMatchResultsReader reader,
            [FromQuery] string? team,
            [FromQuery] string scope = "upcoming",
            CancellationToken ct = default)
        {
            if (!Formax.Application.Services.Matches.TeamSearchTerm.IsSearchable(team))
                return Ok(new { team, scope, count = 0, results = Array.Empty<object>() });

            if (scope is not ("upcoming" or "finished"))
                return BadRequest(new { error = "scope 'upcoming' veya 'finished' olmalı" });

            var results = await reader.SearchByTeamAsync(team!, scope, 100, ct);
            return Ok(new { team, scope, count = results.Count, results });
        }

        [HttpGet("results/days")]
        public async Task<IActionResult> GetResultDays(
            [FromServices] IMatchResultsReader reader,
            [FromQuery] int days = 8,
            CancellationToken ct = default)
            => Ok(new { days = await reader.GetRecentResultDaysAsync(days, ct) });
    }
}