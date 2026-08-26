using Formax.API.Common;
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
                var result = await _useCase.ExecuteAsync(matchId, cancellationToken);

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
    }
}