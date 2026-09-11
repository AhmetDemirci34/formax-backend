using Microsoft.Extensions.Logging;
using Formax.Application.AI.Audit;
using Formax.Application.AI.Context;
using Formax.Application.AI.Contexts;
using Formax.Application.AI.Memory;
using Formax.Application.AI.SelfAudit;
using Formax.Application.Common;
using Formax.Application.DTOs.Live;
using Formax.Application.DTOs.Lineup;
using Formax.Application.DTOs.Nabiz;
using Formax.Application.DTOs.Standings;
using Formax.Domain.Constants;
using Formax.Application.Interfaces;
using Formax.Application.Interfaces.Repositories;
using Formax.Application.States;
using Formax.Application.DTOs.Matches;
using Formax.Application.AI.Radar;
using Formax.Application.Services.Matches;
using Formax.Application.Services.Radar.Intelligence.Scenarios;
using Formax.Application.Services.News.Intelligence;
using Formax.Domain.Entities;
using Formax.Domain.States;

namespace Formax.Application.UseCases
{
    public class GetMatchDetailAIContextUseCase
    {
        private readonly IMatchReadRepository _matchReadRepository;
        private readonly IAIDecisionTraceWriter _aiDecisionTraceWriter;
        private readonly IStateTransitionLogWriter _stateTransitionLogWriter;
        private readonly IAISelfInvalidationLogWriter _aiSelfInvalidationLogWriter;
        private readonly ILastExtendedContextKeyRepository _lastExtendedContextKeyRepository;
        private readonly AIUxStateResolver _aiUxStateResolver;
        private readonly ContextDecayEvaluator _contextDecayEvaluator;
        private readonly ISapmaMotor _sapmaMotor;
        private readonly ITeamReadRepository _teamReadRepository;
        private readonly IMatchLineupRepository _matchLineupRepository;
        private readonly IMatchPlayerStatusRepository _matchPlayerStatusRepository;
        private readonly ILeagueStandingRepository _leagueStandingRepository;
        private readonly ICompetitionContextRepository _competitionContextRepository;
        private readonly IMatchLiveStatsRepository _matchLiveStatsRepository;
        /// <summary>Bitmiş maçın KANONİK olay/istatistik kaydı — salt DB, sağlayıcıya çıkmaz.</summary>
        private readonly IPostMatchDataReader _postMatchData;
        /// <summary>Kalıcı deneme defteri — "en son ne zaman bakıldı" sorusunun kaynağı.</summary>
        private readonly IFixtureSyncRepository _fixtureSync;
        private readonly IMatchMomentumRepository _matchMomentumRepository;
        private readonly IMatchLiveEventIngestionRepository _matchLiveEventRepository;
        private readonly IMatchVideoReader _videoReader;
        private readonly INabizFeedRepository _nabizFeedRepository;
        private readonly IUserMatchFollowRepository _followRepository;
        // Radar v2 — anlatı zenginleştirme (opsiyonel; mevcut Execute akışını bozmaz).
        private readonly MatchIntelligenceContextBuilder _contextBuilder;
        private readonly RadarNarrativePipeline _narrativePipeline;
        // Radar v2.2 — dinamik senaryo motoru.
        private readonly MarketProbabilityEngine _marketEngine;
        private readonly ScenarioRankingService _scenarioRanking;
        // FORMAX AI Evolution — GDP-türevli sinyalleri motora tek context olarak veren katman.
        private readonly IMatchAiContextBuilder _aiContextBuilder;
        // FINAL — Evidence → Reasoning köprüsü (Data Engine v1/v2.1 bağlantısı).
        private readonly Formax.Application.Services.Fixtures.FormaxMatchIdFactory _matchIdFactory;
        private readonly Formax.Application.Interfaces.IMatchEvidenceRepository _evidenceRepository;
        // SON DAKİKA — liste üretiminin TEK yeri (kapılar + sıralama + tekilleştirme).
        private readonly Formax.Application.Services.News.Feed.MatchNewsFeedService _newsFeedService;
        // FAZ 1 — TeamComparison/H2H artık ortak kaynaktan (formül birebir aynı; bkz. MatchComparisonFactory).
        private readonly Formax.Application.Services.Matches.MatchComparisonFactory _comparisonFactory;
        // SEZON KAPSAMI — "bu sezon" ifadesinin tek çözücüsü (30.08.2026 kök neden düzeltmesi).
        private readonly ILeagueSeasonResolver _seasonResolver;
        // İç kaynaklı puan durumu (saatlik projeksiyon + cache). Okuma hesap tetiklemez.
        private readonly ILeagueStandingsService _standingsService;
        private readonly Microsoft.Extensions.Logging.ILogger<GetMatchDetailAIContextUseCase> _logger;

        public GetMatchDetailAIContextUseCase(
            IMatchReadRepository matchReadRepository,
            IAIDecisionTraceWriter aiDecisionTraceWriter,
            IStateTransitionLogWriter stateTransitionLogWriter,
            IAISelfInvalidationLogWriter aiSelfInvalidationLogWriter,
            ILastExtendedContextKeyRepository lastExtendedContextKeyRepository,
            AIUxStateResolver aiUxStateResolver,
            ContextDecayEvaluator contextDecayEvaluator,
            ISapmaMotor sapmaMotor,
            ITeamReadRepository teamReadRepository,
            IMatchLineupRepository matchLineupRepository,
            IMatchPlayerStatusRepository matchPlayerStatusRepository,
            ILeagueStandingRepository leagueStandingRepository,
            ICompetitionContextRepository competitionContextRepository,
            IMatchLiveStatsRepository matchLiveStatsRepository,
            IPostMatchDataReader postMatchData,
            IFixtureSyncRepository fixtureSync,
            IMatchMomentumRepository matchMomentumRepository,
            IMatchLiveEventIngestionRepository matchLiveEventRepository,
            IMatchVideoReader videoReader,
            INabizFeedRepository nabizFeedRepository,
            IUserMatchFollowRepository followRepository,
            MatchIntelligenceContextBuilder contextBuilder,
            RadarNarrativePipeline narrativePipeline,
            MarketProbabilityEngine marketEngine,
            ScenarioRankingService scenarioRanking,
            IMatchAiContextBuilder aiContextBuilder,
            Formax.Application.Services.Fixtures.FormaxMatchIdFactory matchIdFactory,
            Formax.Application.Interfaces.IMatchEvidenceRepository evidenceRepository,
            Formax.Application.Services.Matches.MatchComparisonFactory comparisonFactory,
            Formax.Application.Services.News.Feed.MatchNewsFeedService newsFeedService,
            ILeagueSeasonResolver seasonResolver,
            ILeagueStandingsService standingsService,
            Microsoft.Extensions.Logging.ILogger<GetMatchDetailAIContextUseCase> logger)
        {
            _seasonResolver = seasonResolver;
            _standingsService = standingsService;
            _newsFeedService = newsFeedService;
            _comparisonFactory = comparisonFactory;
            _matchReadRepository = matchReadRepository;
            _aiDecisionTraceWriter = aiDecisionTraceWriter;
            _stateTransitionLogWriter = stateTransitionLogWriter;
            _aiSelfInvalidationLogWriter = aiSelfInvalidationLogWriter;
            _lastExtendedContextKeyRepository = lastExtendedContextKeyRepository;
            _aiUxStateResolver = aiUxStateResolver;
            _contextDecayEvaluator = contextDecayEvaluator;
            _sapmaMotor = sapmaMotor;
            _teamReadRepository = teamReadRepository;
            _matchLineupRepository = matchLineupRepository;
            _matchPlayerStatusRepository = matchPlayerStatusRepository;
            _leagueStandingRepository = leagueStandingRepository;
            _competitionContextRepository = competitionContextRepository;
            _matchLiveStatsRepository = matchLiveStatsRepository;
            _postMatchData = postMatchData;
            _fixtureSync = fixtureSync;
            _matchMomentumRepository = matchMomentumRepository;
            _matchLiveEventRepository = matchLiveEventRepository;
            _videoReader = videoReader;
            _nabizFeedRepository = nabizFeedRepository;
            _followRepository = followRepository;
            _contextBuilder = contextBuilder;
            _narrativePipeline = narrativePipeline;
            _marketEngine = marketEngine;
            _scenarioRanking = scenarioRanking;
            _aiContextBuilder = aiContextBuilder;
            _matchIdFactory = matchIdFactory;
            _evidenceRepository = evidenceRepository;
            _logger = logger;
        }

        // ────────────────────────────────────────────────────────────────────────
        // Public entry point
        // ────────────────────────────────────────────────────────────────────────

        public MatchDetailDto? Execute(int matchId)
        {
            var match = _matchReadRepository
                .Query()
                .FirstOrDefault(x => x.Id == matchId);

            if (match == null)
                return null;

            var now = DateTime.UtcNow;

            // ── AI state pipeline ────────────────────────────────────────────────
            var lastExtendedContext = _lastExtendedContextKeyRepository.GetByMatchId(match.Id);
            var resolvedState = _aiUxStateResolver.Resolve(match, lastExtendedContext);
            var aiUxState = resolvedState;

            if (_contextDecayEvaluator.ShouldSelfRetract(aiUxState, lastExtendedContext))
            {
                aiUxState = AIUxState.SelfRetracted;
                _aiSelfInvalidationLogWriter.Write(new AISelfInvalidationLog
                {
                    MatchId = match.Id,
                    Reason = "extended_context_recently_used",
                    CreatedAt = now
                });
            }

            if (aiUxState != resolvedState)
                _stateTransitionLogWriter.Write(new StateTransitionLog
                {
                    MatchId = match.Id, FromState = resolvedState, ToState = aiUxState,
                    Trigger = "context_decay", CreatedAt = now
                });
            else
                _stateTransitionLogWriter.Write(new StateTransitionLog
                {
                    MatchId = match.Id, FromState = AIUxState.Silent, ToState = aiUxState,
                    Trigger = "context_resolution", CreatedAt = now
                });

            if (aiUxState == AIUxState.Extended)
            {
                var contextKey = match.Status == "Live"
                    ? AIContextKey.LiveMatchExtended
                    : AIContextKey.PreMatchExtended;
                _lastExtendedContextKeyRepository.Upsert(new LastExtendedContextKey
                {
                    MatchId = match.Id, ContextKey = contextKey, ExtendedAt = now
                });
            }

            _aiDecisionTraceWriter.Write(new AIDecisionTrace
            {
                MatchId = match.Id,
                ConfidenceScore = aiUxState switch
                {
                    AIUxState.Extended => 0.65,
                    AIUxState.Short    => 0.40,
                    _                  => 0.25
                },
                GuardrailDecision =
                    aiUxState == AIUxState.Silent || aiUxState == AIUxState.SelfRetracted
                        ? "AI_WITHHELD_CONTEXT"
                        : "AI_CONTEXT_ALLOWED",
                AiBehaviorState   = aiUxState.ToString(),
                MemoryDecayApplied = aiUxState == AIUxState.SelfRetracted,
                CreatedAt         = now
            });

            // ── Core data ────────────────────────────────────────────────────────
            var homeTeam = _teamReadRepository.GetById(match.HomeTeamId);
            var awayTeam = _teamReadRepository.GetById(match.AwayTeamId);

            var homeComparison = BuildTeamComparison(match.HomeTeamId, homeOnly: true);
            var awayComparison = BuildTeamComparison(match.AwayTeamId, awayOnly: true);
            var h2h            = BuildH2H(match.HomeTeamId, match.AwayTeamId, match.Id);
            var insight        = BuildInsight(
                homeComparison, awayComparison,
                homeTeam?.Name ?? "Ev sahibi",
                awayTeam?.Name ?? "Deplasman");

            var sapma = _sapmaMotor.CalculateForListItem(new MatchListItemDto
            {
                MatchId  = match.Id,
                HomeTeam = homeTeam?.Name ?? string.Empty,
                AwayTeam = awayTeam?.Name ?? string.Empty,
                StartTime = match.MatchDate,
                Status   = match.Status
            });

            // ── League positions from standings ──────────────────────────────────
            var seasonYear  = ResolveSeasonYear(match.MatchDate);
            var homeRank = 0;
            var awayRank = 0;
            var allStandings = _leagueStandingRepository.GetByLeague(match.LeagueId, seasonYear);
            if (allStandings.Count > 0)
            {
                // LeagueStandings.TeamId EXTERNAL id'dir; canonical id ile aramak YANLIŞ takımı
                // buluyordu (kimlik uzayları örtüşüyor — bkz. BuildStandingSection, 3588 örneği).
                var homeStandingId = ResolveTeamExternalId(match.HomeTeamId) ?? match.HomeTeamId;
                var awayStandingId = ResolveTeamExternalId(match.AwayTeamId) ?? match.AwayTeamId;
                homeRank = allStandings.FirstOrDefault(s => s.TeamId == homeStandingId)?.Position ?? 0;
                awayRank = allStandings.FirstOrDefault(s => s.TeamId == awayStandingId)?.Position ?? 0;
            }

            // ── Sprint 1: Lineup & player status ─────────────────────────────────
            var lineupSection       = BuildLineupSection(match.Id, match.MatchDate, match.ExternalMatchId);
            var playerStatusSection = BuildPlayerStatusSection(match.Id);

            // ── AI intelligence — Radar v2.2 dinamik senaryo sıralaması ────────────
            var homeNm = homeTeam?.Name ?? "Ev sahibi";
            var awayNm = awayTeam?.Name ?? "Deplasman";
            var gucSkoru = (int)sapma.GucSkoru;

            // FORMAX AI Evolution — motor ham veri yerine tek UnifiedMatchAiContext okur.
            var aiContext = _aiContextBuilder.Build(
                match.Id, match.HomeTeamId, match.AwayTeamId,
                homeComparison, awayComparison, h2h, gucSkoru, homeNm, awayNm);
            var scenarioCandidates = _marketEngine.Evaluate(aiContext);
            var rankedScenarios = _scenarioRanking.RankTop(scenarioCandidates, 3);

            // Geniş havuzdan seçilen en güçlü 3 senaryo → mevcut DTO sözleşmesi (Probabilities).
            // Motor boş dönerse eski deterministik 3-market üretimine düşülür (geri-uyum).
            var probabilities = rankedScenarios.Count > 0
                ? rankedScenarios.Select(c => new ProbabilityItemDto
                    {
                        Market = c.Market, Probability = c.Probability, Confidence = c.Confidence
                    }).ToList()
                : BuildProbabilities(homeComparison, awayComparison, h2h, sapma, homeNm, awayNm);
            var keyMatchups     = BuildKeyMatchups(lineupSection);
            var market          = BuildMarketIntelligence(sapma);
            var risk            = BuildRiskIntelligence(homeComparison, awayComparison);
            var tactical        = BuildTacticalMatchup(homeComparison, awayComparison);

            // ── Watchers ─────────────────────────────────────────────────────────
            var watchersCount = _followRepository.CountByMatchId(match.Id);

            // ── Takımların ULUSAL ligi ("ligde son N maç" filtresi) ───────────────
            var homeFormLeagueId = ResolveTeamLeagueId(
                match.HomeTeamId, ResolveTeamExternalId(match.HomeTeamId), seasonYear);
            var awayFormLeagueId = ResolveTeamLeagueId(
                match.AwayTeamId, ResolveTeamExternalId(match.AwayTeamId), seasonYear);

            // ── SEZON KAPSAMLI FORM (kök neden düzeltmesi) ────────────────────────
            // Liste ve özet AYNI maç kümesinden üretilir: aynı lig + bu sezon + kickoff
            // öncesi + tamamlanmış. Anlatı da bu özetin cümlesini kullanır.
            var homeSeasonForm = BuildSeasonForm(match, match.HomeTeamId, homeNm, homeFormLeagueId);
            var awaySeasonForm = BuildSeasonForm(match, match.AwayTeamId, awayNm, awayFormLeagueId);

            // ── Assemble ─────────────────────────────────────────────────────────
            var aiSummary = aiUxState switch
            {
                AIUxState.Extended    => "Maç bağlamı ve tempo analiz edilebilir seviyeye ulaşmıştır.",
                AIUxState.Short       => "Maç öncesi veri oluşuyor. Şu an ölçüm sınırlı.",
                AIUxState.SelfRetracted => "AI, yakın zamanda yapılan değerlendirme nedeniyle bu aşamada geri çekilmeyi tercih etmiştir.",
                _                     => "Bu maç için AI şu aşamada yönlendirici bir analiz sunmamayı tercih etmiştir."
            };

            // Bitmiş maçın videoları SALT DB'den okunur (arka planda önceden doğrulanmıştır);
            // arama durumu da aynı listeye ve kalıcı deftere bakar.
            var isFinished = string.Equals(match.Status, MatchStatuses.Finished, StringComparison.OrdinalIgnoreCase);
            var finishedVideos = isFinished ? _videoReader.GetVideos(match.Id) : new List<MatchVideoDto>();

            return new MatchDetailDto
            {
                MatchId  = match.Id,
                HomeTeam = new TeamSummaryDto
                {
                    Id      = match.HomeTeamId,
                    Name    = homeTeam?.Name ?? string.Empty,
                    LogoUrl = homeTeam?.LogoUrl,
                    Rank    = homeRank > 0 ? homeRank : null
                },
                AwayTeam = new TeamSummaryDto
                {
                    Id      = match.AwayTeamId,
                    Name    = awayTeam?.Name ?? string.Empty,
                    LogoUrl = awayTeam?.LogoUrl,
                    Rank    = awayRank > 0 ? awayRank : null
                },
                MatchDate    = match.MatchDate,
                Status       = match.Status,
                // ── MAÇ SONRASI ÖZET (02.09.2026) ───────────────────────────────
                // Maç bittiğinde aynı rota özet gösterir. İY/2Y/MS kırılımı ve önemli
                // anlar YALNIZ depodan okunur — bu yol hiçbir sağlayıcıya çıkmaz,
                // dolayısıyla maç detayına tıklamak 0 provider isteği üretir.
                ScoreBreakdown = string.Equals(match.Status, MatchStatuses.Finished, StringComparison.OrdinalIgnoreCase)
                    ? MatchScoreBreakdownDto.From(match, resultIsFinal: true)
                    : null,
                // OLAYLAR — ÖNCE KANONİK KAYIT, sonra eski canlı tablo.
                //
                // Kanonik kayıt (MatchEventRecords) arka plan işinin fixtures/events
                // çekerek yazdığı veridir ve canlı akıştan BAĞIMSIZDIR; canlı veri
                // kapalıyken (LiveMatchData:Enabled=false) tek dolu kaynak odur.
                // Geriye uyum için, kanonik kayıt henüz gelmemiş maçlarda eski
                // MatchLiveEvents okunmaya devam eder — hiçbir maç veri kaybetmez.
                Events       = string.Equals(match.Status, MatchStatuses.Finished, StringComparison.OrdinalIgnoreCase)
                    ? BuildEvents(match.Id)
                    : new List<MatchEventDto>(),
                // Maç videoları SALT DB'den okunur — arka planda önceden doğrulanmıştır.
                // Bu okuma hiçbir sağlayıcıya, arama motoruna veya YouTube'a çıkmaz.
                Videos       = finishedVideos,
                // ARAMA DURUMU — kalıcı defterden; saatten türetilmez. Sıfır dış istek.
                VideoSearch  = isFinished ? BuildVideoSearch(match.ExternalMatchId, finishedVideos) : null,
                // İSTATİSTİK — ÖNCE KANONİK KAYIT (nullable ölçümler), sonra eski tablo.
                // Kanonik satırda sağlayıcının vermediği ölçüm null kalır ve o satır hiç
                // gösterilmez; eski tabloda her alan int olduğu için "veri yok" ile
                // "gerçekten sıfır" ayırt edilemiyordu.
                Statistics   = string.Equals(match.Status, MatchStatuses.Finished, StringComparison.OrdinalIgnoreCase)
                    ? BuildStatistics(match.Id)
                    : null,
                League       = match.League,
                // Sağlayıcının HAM tur adı (teşhis/geri-uyum) ve ondan türeyen Türkçe maç türü.
                Round        = BuildRoundLabel(match),
                MatchTypeLabel = Formax.Application.Services.Matches.MatchTypeLabelResolver
                                     .Resolve(BuildRoundLabel(match)),
                Referee      = match.Referee,
                Venue        = match.Venue,
                Weather      = null,        // reserved — requires dedicated weather API
                WatchersCount = watchersCount,

                // "Ligde son N maç" — takımın KENDİ ulusal ligi (maçın ligi değil: Avrupa
                // kupası maçında da kullanıcı takımın lig formunu görmek ister). Lig
                // çözülemezse filtre uygulanmaz ve DTO'daki lig adı null kalır; UI o zaman
                // başlığı "Son N Maç" yazar — "ligde" demez.
                HomeTeamLastMatches = homeSeasonForm.LastMatches,
                AwayTeamLastMatches = awaySeasonForm.LastMatches,

                // MEVCUT SEZON FORM ÖZETİ — anlatının ve ekranın ortak dayanağı
                // (kaç maç oynandı, G/B/M, iç saha/deplasman, kullanılan MatchId listesi).
                HomeSeasonForm = homeSeasonForm.Summary,
                AwaySeasonForm = awaySeasonForm.Summary,
                HomeTeamFormLeague  = homeFormLeagueId.HasValue ? ResolveLeagueName(homeFormLeagueId.Value) : null,
                AwayTeamFormLeague  = awayFormLeagueId.HasValue ? ResolveLeagueName(awayFormLeagueId.Value) : null,

                Comparison = new ComparisonDto { Home = homeComparison, Away = awayComparison },
                H2H        = h2h,
                Insight    = insight,

                Sapma = new SapmaDto
                {
                    OynanmaSkoru   = sapma.OynanmaSkoru,
                    GucSkoru       = sapma.GucSkoru,
                    Sapma          = sapma.Sapma,
                    OynanmaYonu    = sapma.OynanmaYonu,
                    GercekGucYonu  = sapma.GercekGucYonu,
                    SapmaBolgesi   = sapma.SapmaBolgesi,
                    SessizMi       = sapma.SessizMi,
                    SapmaMetni     = sapma.SapmaMetni
                },

                Ai = new AiDto { State = aiUxState.ToString(), Summary = aiSummary },

                UserProtection = new UserProtectionDto
                {
                    ResponsibilityNote = AIFixedTexts.ResponsibilityNote,
                    DecisionIsYours    = true
                },

                Probabilities   = probabilities,
                KeyMatchups     = keyMatchups,
                MarketIntelligence = market,
                RiskIntelligence   = risk,
                TacticalMatchup    = tactical,

                Lineup       = lineupSection,
                PlayerStatus = playerStatusSection,

                Standing           = BuildStandingSection(match),
                CompetitionContext = BuildCompetitionContextSection(match.Id),

                Live      = BuildLiveSection(match),
                NabizFeed = BuildNabizSection(match.Id)
            };
        }

        // ────────────────────────────────────────────────────────────────────────
        // Radar v2 — AI anlatı ile zenginleştirilmiş giriş noktası.
        // Mevcut sync Execute'u çağırır (akış aynen korunur), sonra LLM anlatısını
        // (Keşfet özeti + Detay section'ları) MatchDetailDto.AiNarrative'e ekler.
        // LLM kapalı/sustuğunda pipeline deterministik fallback döndürür → bozulmaz.
        // ────────────────────────────────────────────────────────────────────────
        public async Task<MatchDetailDto?> ExecuteAsync(int matchId, CancellationToken ct = default)
        {
            // AŞAMA PROFİLİ — /detail yavaşlığının nereden geldiği ÖLÇÜLEREK bulunur.
            // Log seviyesi Information; her aşamanın gerçek ms'i "[DETAIL-PROF]" ile yazılır.
            var sw = System.Diagnostics.Stopwatch.StartNew();
            long tSync = 0, tContext = 0, tEvidence = 0, tNews = 0, tNarrative = 0;

            var detail = Execute(matchId);
            tSync = sw.ElapsedMilliseconds;
            if (detail == null) return null;

            // AI konuşma izni mevcut guardrail kararından gelir (Silent/SelfRetracted → fallback).
            var aiAllowed = detail.Ai.State is "Extended" or "Short";

            // Senaryoları (kanıt etiketleriyle) yeniden sırala — DTO ile aynı deterministik sonuç,
            // ek olarak EvidenceTags taşır → LLM "neden öne çıkıyor"u kanıta dayandırır.
            var aiContext = _aiContextBuilder.Build(
                detail.MatchId, detail.HomeTeam.Id, detail.AwayTeam.Id,
                detail.Comparison.Home, detail.Comparison.Away, detail.H2H,
                detail.Sapma.GucSkoru, detail.HomeTeam.Name, detail.AwayTeam.Name);
            var candidates = _marketEngine.Evaluate(aiContext);
            var ranked = _scenarioRanking.RankTop(candidates, 3);

            // FINAL köprü: maçın FORMAX_MATCH_ID'sini hesapla → Evidence Store'dan
            // Match Intelligence Context çek. Evidence varsa Reasoning ham haber yerine
            // signal-typed kanıtlardan beslenir; yoksa eski NABIZ'e düşer (geri-uyum).
            var formaxMatchId = _matchIdFactory.Create(
                detail.MatchDate, detail.HomeTeam.Name, detail.AwayTeam.Name);
            // Kickoff da verilir → haberin maça göre zaman konumu (maç öncesi / maç günü /
            // eski) okuma anında belirlenir; geçmiş sezona ait içerik bugünkü maçın "son
            // gelişmesi" olarak anlatıya giremez.
            //
            // BİLİNEN KADRO ADLARI: haber olayının öznesi bir oyuncuysa adı YALNIZ bu
            // listeyle eşleştiğinde taşınır (serbest ad çıkarımı yok). Yeni sorgu/kaynak
            // YOK — bu maç için zaten çekilmiş oyuncu durumu kullanılır.
            var knownPlayers = (detail.PlayerStatus?.Injuries ?? new List<PlayerStatusDto>())
                .Concat(detail.PlayerStatus?.Suspensions ?? new List<PlayerStatusDto>())
                .Concat(detail.PlayerStatus?.Doubtful ?? new List<PlayerStatusDto>())
                .Select(p => p.PlayerName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            tContext = sw.ElapsedMilliseconds;

            var evidenceCtx = await _evidenceRepository.GetContextAsync(
                formaxMatchId, detail.HomeTeam.Name, detail.AwayTeam.Name, detail.MatchDate,
                knownPlayers, ct);
            tEvidence = sw.ElapsedMilliseconds;

            // SON DAKİKA — maçın gerçek haberleri. Aynı FORMAX_MATCH_ID zaten yukarıda
            // hesaplandı; ikinci bir kimlik/toplama yolu açılmaz.
            detail.NabizFeed = await BuildNewsSectionAsync(formaxMatchId, detail, ct);
            tNews = sw.ElapsedMilliseconds;

            // Availability: aiContext'te ZATEN hesaplanmış (MatchPlayerStatuses + canonical/external
            // takım kimliği çözümü orada). Yeniden hesaplamak yerine aynı sonuç pack'e taşınır.
            // ANLATI TARAFI TAM KAYDI GÖRÜR: motorun dar penceresi (aiContext.Availability)
            // yerine kickoff uzaklığından bağımsız AvailabilityFull taşınır. Motor bu alanı
            // okumaz → Olası Sonuçlar bu bağlantıdan etkilenmez.
            var context = _contextBuilder.Build(detail, null, ranked, evidenceCtx, aiContext.AvailabilityFull);

            // ── ANLATI ARTIK CEVABI BLOKLAMAZ (ÖLÇÜLDÜ 03.09.2026) ─────────────────
            //
            // KÖK NEDEN: bu uç, önbelleği soğuk bir maçta bulut LLM'i BEKLİYORDU. Aşama
            // profili (aynı istek):
            //     sync=130ms context=15ms evidence=2ms news=37ms
            //     narrative(LLM)=13.100ms  → total=13.284ms
            // Yani ekranın gerçekten ihtiyaç duyduğu her şey ~184 ms'de hazırdı; kalan
            // 13 saniye yalnız anlatı beklemesiydi. Soğuk LLM süresi 5,7–13,1 sn arasında
            // dalgalanıyor; istemci zaman aşımı 10 sn. 10 sn'yi aştığı anlarda istek
            // iptal ediliyor ve kullanıcı "Maç bilgileri şu an yüklenemiyor" görüyordu.
            // Yenileyince anlatı artık önbellekte olduğu için 0,2 sn'de açılıyordu —
            // "bazen açılmıyor, yenileyince düzeliyor" şikâyetinin tam tarifi.
            //
            // ÇÖZÜM ZAMAN AŞIMINI BÜYÜTMEK DEĞİLDİR: o, kullanıcıyı 13 saniye spinner'a
            // baktırmak olurdu. Bunun yerine anlatı için KISA BİR BÜTÇE beklenir; bütçe
            // dolarsa cevap anlatısız döner ve üretim ARKA PLANDA sürer. Pipeline sonucu
            // önbelleğe yazdığı için bir sonraki açılış hazır bulur.
            //
            // BİTMİŞ MAÇTA HİÇ ÜRETİLMEZ: kilitli "Bitmiş Maç Özeti" ekranı anlatı
            // GÖSTERMEZ (ürün kararı). Görünmeyecek bir metin için bulut LLM beklemek
            // hem kullanıcıyı bekletir hem boşuna maliyettir.
            var isFinished = string.Equals(detail.Status, MatchStatuses.Finished,
                StringComparison.OrdinalIgnoreCase);

            if (!isFinished)
            {
                // Görevler İSTEK TOKEN'INA BAĞLANMAZ: cevap döndükten sonra da devam edip
                // önbelleği ısıtmalıdırlar. İstekle birlikte iptal edilselerdi, bütçeyi
                // aşan her açılış önbelleği boş bırakır ve sorun kendini tekrarlardı.
                var discoverTask = _narrativePipeline.GenerateAsync(context, RadarSurface.Discover, aiAllowed, CancellationToken.None);
                var reportTask   = _narrativePipeline.GenerateAsync(context, RadarSurface.MatchDetail, aiAllowed, CancellationToken.None);
                var inceleTask   = _narrativePipeline.GenerateAsync(context, RadarSurface.AiIncele, aiAllowed, CancellationToken.None);

                var all = Task.WhenAll(discoverTask, reportTask, inceleTask);
                var finishedInBudget = await Task.WhenAny(all, Task.Delay(NarrativeBudget, ct))
                                                 .ConfigureAwait(false) == all;

                if (finishedInBudget && all.IsCompletedSuccessfully)
                {
                    ApplyNarrative(detail, discoverTask.Result, reportTask.Result, inceleTask.Result);
                }
                else
                {
                    // Arka planda sürecek görevin hatası GÖZLENİR: gözlenmeyen istisna
                    // süreç düzeyinde patlama riskidir ve sessizce kaybolur.
                    _ = all.ContinueWith(
                        t => _logger.LogWarning(t.Exception,
                            "[DETAIL] {MatchId} anlatısı arka planda tamamlanamadı.", matchId),
                        CancellationToken.None,
                        TaskContinuationOptions.OnlyOnFaulted,
                        TaskScheduler.Default);

                    _logger.LogInformation(
                        "[DETAIL] {MatchId} anlatı bütçesi ({Budget} ms) doldu — cevap anlatısız döndü, üretim arka planda sürüyor.",
                        matchId, NarrativeBudget.TotalMilliseconds);
                }
            }

            tNarrative = sw.ElapsedMilliseconds;

            _logger.LogInformation(
                "[DETAIL-PROF] match={MatchId} sync={Sync}ms context={Context}ms evidence={Evidence}ms " +
                "news={News}ms narrative(LLM)={Narrative}ms total={Total}ms",
                matchId, tSync, tContext - tSync, tEvidence - tContext,
                tNews - tEvidence, tNarrative - tNews, tNarrative);

            return detail;
        }

        /// <summary>
        /// ANLATI BÜTÇESİ — cevabın anlatı için bekleyebileceği EN UZUN süre.
        ///
        /// Ölçüm (03.09.2026): anlatı dışındaki her şey ~184 ms; soğuk LLM 5,7–13,1 sn.
        /// Sıcak (önbellekli) anlatı ~0,2 sn içinde döner, yani bu bütçe normal akışta
        /// hiç devreye girmez — yalnız soğuk çağrıda cevabı kurtarır.
        /// </summary>
        private static readonly TimeSpan NarrativeBudget = TimeSpan.FromSeconds(3);

        /// <summary>Üç yüzeyin sonucunu DTO'ya taşır (içerik ve alanlar DEĞİŞMEDİ).</summary>
        private static void ApplyNarrative(
            MatchDetailDto detail,
            Formax.Application.AI.Radar.RadarNarrativeResult discover,
            Formax.Application.AI.Radar.RadarNarrativeResult report,
            Formax.Application.AI.Radar.RadarNarrativeResult incele)
        {
            detail.AiNarrative = new RadarNarrativeDto
            {
                RadarSummary       = discover.RadarSummary,
                Highlights         = discover.Highlights,
                MatchReport        = report.MatchReport,
                WhyThisMatch       = report.WhyThisMatch,
                ReasoningSummary   = report.ReasoningSummary,
                NewsSummary        = report.NewsSummary,
                SocialSummary      = report.SocialSummary,
                StatisticalSummary = report.StatisticalSummary,
                KeyInsights        = report.KeyInsights,
                Scenarios          = report.ScenarioExplanations
                    .Select(s => new RadarScenarioReasonDto { Market = s.Market, Reason = s.Reason })
                    .ToList(),
                EvidenceSummary    = report.EvidenceSummary,
                AiIncele           = incele.AiIncele,
                ReasoningConfidence = report.ReasoningConfidence,
                IsAiGenerated      = discover.IsAiGenerated || report.IsAiGenerated || incele.IsAiGenerated
            };
        }

        // ────────────────────────────────────────────────────────────────────────
        // Comparison & form helpers
        // ────────────────────────────────────────────────────────────────────────

        // Hesap ORTAK kaynağa taşındı (MatchComparisonFactory) — formül birebir aynıdır.
        // Böylece bu ekran ile Discover/Decision/Voice/Narrative/Signals AYNI veriyi görür.
        private TeamComparisonDto BuildTeamComparison(int teamId, bool homeOnly = false, bool awayOnly = false)
            => _comparisonFactory.BuildTeamComparison(teamId, homeOnly, awayOnly);

        /// <summary>
        /// SEZON KAPSAMLI FORM — hem ekrandaki maç listesi hem de anlatının dayandığı özet.
        ///
        /// KÖK NEDEN (ölçüldü 30.08.2026, Barcelona–Rayo Vallecano / La Liga):
        /// eski <c>BuildLastMatches</c> lig + "oynanmış" süzüyor, SEZON süzmüyordu. Barcelona'nın
        /// listesi 1 tane 2026/27 (23.08 Elche 0-5) + 4 tane 2025/26 (10–23 Mayıs) maçından
        /// oluşuyor, ekranda "bu sezonun formu" gibi okunuyordu.
        ///
        /// Artık kapsam kesindir: aynı LeagueId + aynı sezon + sezon başlangıcından sonra +
        /// bu maçın kickoff'undan önce + Status=Finished. Eksik maç ÖNCEKİ SEZONDAN,
        /// hazırlık maçından, kupadan veya Avrupa maçından TAMAMLANMAZ.
        ///
        /// Sezon çözülemezse (ligde o pencerede hiç kayıt yok) liste BOŞ döner ve özet
        /// "veri yok" olur — tarih veya sezon TAHMİN EDİLMEZ.
        /// </summary>
        private SeasonFormResult BuildSeasonForm(
            Domain.Entities.Match match,
            int teamId,
            string teamName,
            int? leagueId)
        {
            // Kapsam ligi: önce takımın ULUSAL ligi (Avrupa kupası maçında da kullanıcı lig
            // formunu görmek ister). Çözülemezse MAÇIN KENDİ ligine düşülür — bu bir tahmin
            // değil, maçın gerçek turnuvasıdır ve cümlede adı geçer. Hiçbir durumda
            // "lig" kapsamı olmadan (tüm turnuvalar karışık) form üretilmez.
            var scopeLeagueId = leagueId ?? (match.LeagueId > 0 ? match.LeagueId : (int?)null);
            var leagueName = scopeLeagueId.HasValue ? ResolveLeagueName(scopeLeagueId.Value) : string.Empty;

            if (!scopeLeagueId.HasValue)
            {
                return new SeasonFormResult(
                    new List<LastMatchDto>(),
                    TeamSeasonFormService.Empty(teamId, teamName, leagueName),
                    $"LEAGUE_NOT_RESOLVED: TeamId={teamId} için lig kapsamı çözülemedi.");
            }

            var resolution = _seasonResolver.Resolve(scopeLeagueId.Value, match.MatchDate);
            if (!resolution.Resolved)
            {
                _logger.LogWarning("Season scope missing for form — {Error}", resolution.Error);
                return new SeasonFormResult(
                    new List<LastMatchDto>(),
                    TeamSeasonFormService.Empty(teamId, teamName, leagueName),
                    resolution.Error);
            }

            var scope = resolution.Scope!;

            // Kapsamın üst sınırı BU MAÇIN başlama anıdır: sonrasında oynanan bir maç
            // (varsa) bu analizin girdisi olamaz.
            var settled = _matchReadRepository.GetSeasonLeagueMatchesForTeam(
                teamId, scopeLeagueId.Value, scope.StartUtc, match.MatchDate);

            // ── İKİ AYRI KAVRAM (06.09.2026) ────────────────────────────────────
            //
            // 1) LeagueDataCompleteness — LİGİN bu sezonki verisi tam mı? TEŞHİS'tir;
            //    puan durumu tablosunun güncelliğini anlatır.
            // 2) TeamFormSampleQuality — BU TAKIMIN örneklemi ne kadar sağlam? Form
            //    anlatısının tek kapısı budur.
            //
            // Eskiden (1) doğrudan (2)'nin kapısıydı: Süper Lig'de sonucu kesinleşmemiş
            // TEK maç (Başakşehir–Galatasaray, 04.09) yüzünden Trabzonspor ile
            // Gençlerbirliği'nin 4'er maçlık TAM formu gizleniyor, yerine teknik bir
            // uyarı basılıyordu. Artık ligin tamlığı yalnız teşhis olarak taşınır;
            // sınırlama YALNIZ incelenen takımın kendi eksik sonucundan doğar.
            var nowUtc = DateTime.UtcNow;
            var seasonFixtures = _matchReadRepository.GetSeasonLeagueFixturesBefore(
                scopeLeagueId.Value, scope.StartUtc, scope.EndUtc, nowUtc);

            var completeness = Formax.Application.Services.Standings.SeasonDataCompleteness.Evaluate(
                seasonFixtures, nowUtc);

            var teamMissing = TeamFormSampleQuality.MissingResultMatchIdsFor(
                teamId, seasonFixtures, nowUtc);

            // Görüntülenen maç kendi form listesine giremez (aynı tarihli kayıt tekrarı).
            settled = settled.Where(m => m.Id != match.Id).ToList();

            var summary = TeamSeasonFormService.Build(
                teamId, teamName, leagueName, scope, match.MatchDate, settled,
                completeness, teamMissing);

            var lastMatches = new List<LastMatchDto>(settled.Count);
            foreach (var m in settled)
            {
                var isHome = m.HomeTeamId == teamId;
                var gf = isHome ? m.HomeScore : m.AwayScore;
                var ga = isHome ? m.AwayScore : m.HomeScore;

                lastMatches.Add(new LastMatchDto
                {
                    MatchId     = m.Id,
                    Opponent    = isHome
                        ? (m.AwayTeam?.Name ?? string.Empty)
                        : (m.HomeTeam?.Name ?? string.Empty),
                    Result      = gf > ga ? "W" : gf == ga ? "D" : "L",
                    Score       = $"{gf}-{ga}",
                    Date        = m.MatchDate.ToString("dd.MM.yyyy"),
                    Competition = m.League,
                    IsHome      = isHome,
                    // İlk yarı MAÇIN yönünde taşınır (ev - deplasman); takım perspektifine
                    // çevrilmez. UI satırı zaten ev/deplasman sütunlarıyla kuruluyor.
                    HalfTimeHomeScore = m.HalfTimeHomeScore,
                    HalfTimeAwayScore = m.HalfTimeAwayScore
                });
            }

            return new SeasonFormResult(lastMatches, summary, null);
        }

        /// <summary>Form listesi + sezon özeti + (varsa) kapsam hatası.</summary>
        private sealed record SeasonFormResult(
            List<LastMatchDto> LastMatches,
            TeamSeasonFormDto Summary,
            string? Error);

        // Hesap ORTAK kaynağa taşındı (MatchComparisonFactory) — formül birebir aynıdır.
        private H2HDto BuildH2H(int homeTeamId, int awayTeamId, int? excludeMatchId = null)
            => _comparisonFactory.BuildH2H(homeTeamId, awayTeamId, excludeMatchId);

        // ────────────────────────────────────────────────────────────────────────
        // Insight builder — turns the already-computed TeamComparisonDto fields
        // (form, goals scored/conceded, clean sheets, scoring rate, league rank)
        // into concrete, team-named, number-backed sentences.
        // No LLM / no random story — every number is a real computed value, and
        // the same match always yields the same text (deterministic selection).
        // ────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Karşılaştırmalı bir cümle kurmak için HER İKİ takımda gereken en az gerçek maç sayısı.
        ///
        /// DEĞER ARTIK BURADA TANIMLI DEĞİL: eşik tüm yorum yüzeyleri için tek yerde
        /// (<see cref="FormEvidencePolicy.MinSample"/>) tutulur. Aynı sayı Outlook, Reasoning
        /// paketi ve çıkış guard'ı tarafından da okunur; yüzeyler ayrışamaz.
        /// </summary>
        private const int MinComparableSample = FormEvidencePolicy.MinSample;

        /// <summary>
        /// Yüzde değerine gelen iyelik+bulunma eki ("%90'ında", "%80'inde").
        /// Ek, sayının OKUNUŞUNA göre değişir; sabit "'inde" yazmak %90/%40/%60'ta hatalıydı.
        /// Birler basamağı varsa onun okunuşu, yoksa onlar basamağının okunuşu belirler.
        /// </summary>
        private static string PercentSuffix(int value)
        {
            var n = Math.Clamp(value, 0, 100);
            if (n == 100) return "ünde";                       // yüz
            if (n % 10 != 0)
                return (n % 10) switch                          // bir, iki, üç...
                {
                    1 => "inde", 2 => "sinde", 3 => "ünde", 4 => "ünde", 5 => "inde",
                    6 => "sında", 7 => "sinde", 8 => "inde", _ => "unda"
                };
            return (n / 10) switch                              // on, yirmi, otuz...
            {
                0 => "ında", 1 => "unda", 2 => "sinde", 3 => "unda", 4 => "ında",
                5 => "sinde", 6 => "ında", 7 => "inde", 8 => "inde", _ => "ında"
            };
        }

        private InsightDto BuildInsight(
            TeamComparisonDto home, TeamComparisonDto away,
            string homeName, string awayName)
        {
            // VERİ YETERLİLİĞİ KAPISI — karşılaştırmalı her cümle İKİ tarafın da yeterli
            // örneğine dayanmalıdır. Eksik veri "0" değildir: bir takımın kaydı yokken tüm
            // oranları 0 dönüyordu ve aşağıdaki ayrışma hesabı bunu gerçek bir fark sanıp
            // "X %90 ile üstün" gibi yanıltıcı cümle üretiyordu (ölçüldü: Deportivo 0 maç).
            var comparable = home.SampleCount >= MinComparableSample
                             && away.SampleCount >= MinComparableSample;

            if (!comparable)
            {
                // Tek taraflı/yetersiz veriyle üstünlük iddiası KURULMAZ. Başlık yalnız
                // doğrulanabilir olguyu söyler; özet BOŞ bırakılır → UI bloğu hiç göstermez
                // (uydurma cümle yok, "veri yok" duyurusu da yapılmaz).
                return new InsightDto
                {
                    Headline = $"{homeName} sahasında {awayName} ile karşılaşıyor.",
                    Summary  = string.Empty
                };
            }

            // ── HEADLINE: rank context first (when available), else form gap ──
            string headline;
            var rankHeadline = BuildRankHeadline(home.LeagueRank, away.LeagueRank, homeName, awayName);
            if (rankHeadline != null)
            {
                headline = rankHeadline;
            }
            else
            {
                var formGap = home.FormScore - away.FormScore;
                if (formGap >= 4)
                    headline = $"{homeName} son maçlarındaki formuyla öne çıkıyor.";
                else if (formGap <= -4)
                    headline = $"{awayName} daha iyi bir form çizgisiyle geliyor.";
                else if (formGap > 0)
                    headline = $"{homeName} formda hafif önde, ama fark dar.";
                else if (formGap < 0)
                    headline = $"{awayName} formda hafif önde, ama fark dar.";
                else
                    headline = $"{homeName} ve {awayName} form açısından başa baş.";
            }

            // ── SUMMARY: pick the most distinctive real signal between the two.
            // Candidates are scored by how separated the two teams are; the widest
            // gap wins → different matches surface different stories.
            var summary = BuildSummary(home, away, homeName, awayName);

            return new InsightDto { Headline = headline, Summary = summary };
        }

        private static string? BuildRankHeadline(int homeRank, int awayRank, string homeName, string awayName)
        {
            if (homeRank <= 0 && awayRank <= 0) return null;

            // Leader on the pitch
            if (homeRank == 1) return $"Lider {homeName}, sahasında {awayName}'i ağırlıyor.";
            if (awayRank == 1) return $"Lider {awayName}, deplasmanda {homeName} karşısında.";

            // Both ranked → contextual clash
            if (homeRank > 0 && awayRank > 0)
            {
                var top = Math.Min(homeRank, awayRank);
                var gap = Math.Abs(homeRank - awayRank);
                if (top <= 3 && gap <= 2)
                    return $"Üst sıra mücadelesi: {homeName} (#{homeRank}) — {awayName} (#{awayRank}).";
                if (gap <= 2)
                    return $"Sıralamada komşu iki takım: #{homeRank} {homeName} ile #{awayRank} {awayName}.";
                return $"#{homeRank} {homeName}, #{awayRank} {awayName} ile karşılaşıyor.";
            }

            // Only one side ranked
            var rankedName = homeRank > 0 ? homeName : awayName;
            var rank = homeRank > 0 ? homeRank : awayRank;
            return $"{rankedName} ligde #{rank} sırada bu maça çıkıyor.";
        }

        private static string BuildSummary(
            TeamComparisonDto home, TeamComparisonDto away,
            string homeName, string awayName)
        {
            // Build candidate sentences, each tagged with a "separation" weight
            // (only real, computed numbers — nothing invented).
            var candidates = new List<(double weight, string text)>();

            // 1. Attack output gap
            var atkGap = Math.Abs(home.AvgGoalsFor - away.AvgGoalsFor);
            if (atkGap >= 0.4)
            {
                var lead = home.AvgGoalsFor > away.AvgGoalsFor ? homeName : awayName;
                candidates.Add((atkGap, $"{lead} son maçlarda daha üretken: {homeName} {home.AvgGoalsFor:0.0} gol ortalamasındayken {awayName} {away.AvgGoalsFor:0.0} ortalamada."));
            }

            // 2. Defensive solidity (clean sheets)
            var csGap = Math.Abs(home.CleanSheetRate - away.CleanSheetRate);
            if (csGap >= 20)
            {
                var solid = home.CleanSheetRate > away.CleanSheetRate ? homeName : awayName;
                var rate = Math.Max(home.CleanSheetRate, away.CleanSheetRate);
                candidates.Add((csGap / 20.0, $"{solid} savunmada daha güvenli: son maçlarının %{rate}'{PercentSuffix(rate)} gol yemedi."));
            }

            // 3. Defensive weakness (goals conceded)
            var defGap = Math.Abs(home.AvgGoalsAgainst - away.AvgGoalsAgainst);
            if (defGap >= 0.4)
            {
                var leaky = home.AvgGoalsAgainst > away.AvgGoalsAgainst ? homeName : awayName;
                var conceded = Math.Max(home.AvgGoalsAgainst, away.AvgGoalsAgainst);
                candidates.Add((defGap, $"{leaky} savunması zorlanıyor: maç başına {conceded:0.0} gol yiyor."));
            }

            // 4. Scoring consistency
            var srGap = Math.Abs(home.GoalScoringRate - away.GoalScoringRate);
            if (srGap >= 20)
            {
                var consistent = home.GoalScoringRate > away.GoalScoringRate ? homeName : awayName;
                var rate = Math.Max(home.GoalScoringRate, away.GoalScoringRate);
                candidates.Add((srGap / 20.0, $"{consistent} istikrarlı skor üretiyor: son maçlarının %{rate}'{PercentSuffix(rate)} gol attı."));
            }

            if (candidates.Count == 0)
            {
                // Real data exists but the two teams are statistically close.
                return $"{homeName} ile {awayName} istatistiksel olarak birbirine yakın; ince farklar belirleyici olabilir.";
            }

            // Most separated signal wins → distinct matches get distinct stories.
            return candidates.OrderByDescending(c => c.weight).First().text;
        }

        // ────────────────────────────────────────────────────────────────────────
        // AI intelligence builders
        // ────────────────────────────────────────────────────────────────────────

        private List<ProbabilityItemDto> BuildProbabilities(
            TeamComparisonDto home,
            TeamComparisonDto away,
            H2HDto h2h,
            dynamic sapma,
            string homeName,
            string awayName)
        {
            // KG VAR — derived from both teams' scoring + H2H scoring records
            var avgGoalsPerH2H = h2h.Matches.Count > 0
                ? h2h.Matches.Average(m => m.HomeScore + m.AwayScore)
                : (home.AvgGoalsFor + away.AvgGoalsFor);

            var bothScoreProb = (int)Math.Min(95, Math.Max(25,
                ((home.GoalScoringRate + away.GoalScoringRate) / 2.0) +
                (avgGoalsPerH2H > 2 ? 10 : 0)));

            // 2.5 ÜST — derived from avg total goals
            var avgTotal = home.AvgGoalsFor + away.AvgGoalsFor;
            var over25Prob = (int)Math.Min(95, Math.Max(25,
                avgTotal >= 2.5 ? 55 + (int)((avgTotal - 2.5) * 12) : 40));

            // TEAM-SAFE — "X kaybetmez". The favoured side comes from GucSkoru
            // (>50 = home favoured, <50 = away). Real team name, not a hardcoded "GS".
            var gucSkoru = (int)(sapma.GucSkoru ?? 50);
            var favourHome = gucSkoru >= 50;
            var safeProb = (int)Math.Min(95, Math.Max(25, 40 + Math.Abs(gucSkoru - 50) / 3));
            var safeName = ShortTeamLabel(favourHome ? homeName : awayName);

            return new List<ProbabilityItemDto>
            {
                new() { Market = "KG Var",                  Probability = bothScoreProb, Confidence = TierLabel(bothScoreProb) },
                new() { Market = "2.5 Üst",                 Probability = over25Prob,    Confidence = TierLabel(over25Prob)    },
                new() { Market = $"{safeName} Kaybetmez",   Probability = safeProb,      Confidence = TierLabel(safeProb)      }
            };
        }

        // Compact label for a team in a market chip (first token, capped length).
        private static string ShortTeamLabel(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Ev sahibi";
            var first = name.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
            return first.Length > 14 ? first[..14] : first;
        }

        private static string TierLabel(int prob)
            => prob >= 70 ? "YÜKSEK" : prob >= 55 ? "ORTA" : "DÜŞÜK";

        private List<KeyMatchupDto> BuildKeyMatchups(LineupSectionDto lineup)
        {
            if (!lineup.LineupsAnnounced)
                return new List<KeyMatchupDto>();

            // Pair forwards/attackers from home vs defenders from away and vice-versa
            var homeForwards  = lineup.HomeStartingXI.Where(p => p.Position == "F").Take(2).ToList();
            var awayDefenders = lineup.AwayStartingXI.Where(p => p.Position == "D").Take(2).ToList();
            var awayForwards  = lineup.AwayStartingXI.Where(p => p.Position == "F").Take(2).ToList();
            var homeDefenders = lineup.HomeStartingXI.Where(p => p.Position == "D").Take(2).ToList();

            var matchups = new List<KeyMatchupDto>();

            // Home attacker vs Away defender
            for (var i = 0; i < Math.Min(homeForwards.Count, awayDefenders.Count); i++)
            {
                matchups.Add(new KeyMatchupDto
                {
                    HomePlayer    = homeForwards[i].PlayerName,
                    AwayPlayer    = awayDefenders[i].PlayerName,
                    HomePosition  = "F",
                    AwayPosition  = "D",
                    MatchupContext = "Hücum vs Savunma"
                });
            }

            // Away attacker vs Home defender
            for (var i = 0; i < Math.Min(awayForwards.Count, homeDefenders.Count) && matchups.Count < 3; i++)
            {
                matchups.Add(new KeyMatchupDto
                {
                    HomePlayer    = homeDefenders[i].PlayerName,
                    AwayPlayer    = awayForwards[i].PlayerName,
                    HomePosition  = "D",
                    AwayPosition  = "F",
                    MatchupContext = "Savunma vs Hücum"
                });
            }

            return matchups;
        }

        private MarketIntelligenceDto BuildMarketIntelligence(dynamic sapma)
        {
            var oynanma = (int)(sapma.OynanmaSkoru ?? 50);
            var oynanmaYonu = (string)(sapma.OynanmaYonu ?? "FLAT");

            string headline, detail, tone;

            if (oynanmaYonu == "Home" && oynanma >= 65)
            {
                headline = "Piyasada güçlü hareketlilik tespit edildi";
                detail   = $"Son 3 saatte bu maçın oranlarında %{Math.Abs(oynanma - 50) / 2}lık değişim var. Piyasa ev sahibine yakın.";
                tone     = "positive";
            }
            else if (oynanmaYonu == "Away" && oynanma <= 35)
            {
                headline = "Piyasada deplasman yönünde hareket var";
                detail   = $"Piyasa ağırlığı deplasman takımına doğru kayıyor. Oranlar son saatte değişti.";
                tone     = "negative";
            }
            else
            {
                headline = "Piyasa dengeli seyrediyor";
                detail   = "Oranlar son 3 saatte büyük bir hareket göstermedi. Piyasa bekleme modunda.";
                tone     = "neutral";
            }

            return new MarketIntelligenceDto { Headline = headline, Detail = detail, Tone = tone };
        }

        private RiskIntelligenceDto BuildRiskIntelligence(TeamComparisonDto home, TeamComparisonDto away)
        {
            var homeRiskLabel = home.AvgGoalsAgainst >= 1.5
                ? "Sol bek arkası riskli"
                : home.CleanSheetRate < 30
                    ? "Savunma tutarsız"
                    : "Savunma stabil";

            var awayRiskLabel = away.AvgGoalsAgainst >= 1.5
                ? "Geçiş savunması zayıf"
                : away.CleanSheetRate < 30
                    ? "Deplasman savunması açık"
                    : "Savunma dengeli";

            var homeRiskDetail = $"Son 10 maçta maç başı {home.AvgGoalsAgainst:F1} gol yendi.";
            var awayRiskDetail = $"Son 10 maçta maç başı {away.AvgGoalsAgainst:F1} gol yendi.";

            return new RiskIntelligenceDto
            {
                HomeRiskLabel  = homeRiskLabel,
                HomeRiskDetail = homeRiskDetail,
                AwayRiskLabel  = awayRiskLabel,
                AwayRiskDetail = awayRiskDetail
            };
        }

        private TacticalMatchupDto BuildTacticalMatchup(TeamComparisonDto home, TeamComparisonDto away)
        {
            double Norm(double val, double max) => Math.Min(10.0, Math.Round(val / max * 10, 1));

            var maxGoals = Math.Max(home.AvgGoalsFor, away.AvgGoalsFor);
            maxGoals = maxGoals < 0.1 ? 2.5 : maxGoals;

            var homeFormNorm = Math.Min(10.0, Math.Round(home.FormScore / 3.0, 1));
            var awayFormNorm = Math.Min(10.0, Math.Round(away.FormScore / 3.0, 1));

            return new TacticalMatchupDto
            {
                Attack      = new() { Label = "Hücum Gücü",   HomeScore = Norm(home.AvgGoalsFor, maxGoals),           AwayScore = Norm(away.AvgGoalsFor, maxGoals) },
                Defense     = new() { Label = "Savunma",       HomeScore = Math.Min(10, 10 - home.AvgGoalsAgainst * 2), AwayScore = Math.Min(10, 10 - away.AvgGoalsAgainst * 2) },
                Transition  = new() { Label = "Geçiş Tehdidi", HomeScore = Norm(home.GoalScoringRate, 100),            AwayScore = Norm(away.GoalScoringRate, 100) },
                SetPiece    = new() { Label = "Set Parçası",   HomeScore = Math.Round(5 + (home.FormScore % 3) * 0.8, 1), AwayScore = Math.Round(5 + (away.FormScore % 3) * 0.8, 1) },
                Form        = new() { Label = "Form",          HomeScore = homeFormNorm,                               AwayScore = awayFormNorm },
                Discipline  = new() { Label = "Disiplin",      HomeScore = Math.Round(5 + home.CleanSheetRate / 20.0, 1), AwayScore = Math.Round(5 + away.CleanSheetRate / 20.0, 1) }
            };
        }

        /// <summary>
        /// Maçın GERÇEK tur/aşama adı. Öncelik sağlayıcının fikstürle birlikte yazdığı
        /// <c>Match.Round</c>'tur (tüm maçlarda mevcut); yoksa Competition Context'in
        /// StageName'ine düşülür (yalnız birkaç maçta dolu). Hiçbiri yoksa null — tahmin YOK.
        /// </summary>
        private string? BuildRoundLabel(Match match)
        {
            if (!string.IsNullOrWhiteSpace(match.Round)) return match.Round.Trim();
            var ctx = _competitionContextRepository.GetByMatchId(match.Id);
            return string.IsNullOrWhiteSpace(ctx?.StageName) ? null : ctx!.StageName.Trim();
        }

        // ────────────────────────────────────────────────────────────────────────
        // Sprint 1: Lineup builders
        // ────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// BİTMİŞ MAÇ OLAYLARI — kanonik kayıt öncelikli, eski canlı tablo yedek.
        /// İki kaynak KARIŞTIRILMAZ: kanonik kayıt varsa yalnız o kullanılır, aksi hâlde
        /// yalnız eski tablo. Karıştırmak aynı golü iki farklı imzayla iki kez gösterirdi.
        /// </summary>
        private List<MatchEventDto> BuildEvents(int matchId)
        {
            var canonical = _postMatchData.GetEvents(matchId);
            return canonical.Count > 0
                ? MatchEventDto.FromRecords(canonical)
                : MatchEventDto.FromEvents(_matchLiveEventRepository.GetByMatchId(matchId));
        }

        /// <summary>
        /// BİTMİŞ MAÇ İSTATİSTİKLERİ — kanonik kayıt öncelikli, eski canlı tablo yedek.
        /// Kanonik satırlarda GERÇEK ölçüm yoksa (hepsi null) yedeğe düşülür; boş bir
        /// tablo "0-0 istatistik" olarak GÖSTERİLMEZ.
        /// </summary>
        private MatchStatisticsDto? BuildStatistics(int matchId)
        {
            var rows = _postMatchData.GetTeamStatistics(matchId);
            var home = rows.FirstOrDefault(r => r.Side == "Home");
            var away = rows.FirstOrDefault(r => r.Side == "Away");

            var canonical = MatchStatisticsDto.FromTeamRows(home, away);
            return canonical ?? MatchStatisticsDto.From(_matchLiveStatsRepository.GetByMatchId(matchId));
        }

        /// <summary>
        /// RESMÎ ÖZET ARAMASININ DURUMU — kural <see cref="Services.PostMatch.PostMatchVideoSearchStatus"/>.
        /// Defter okuması salt DB'dir; bu metot hiçbir sağlayıcıya çıkmaz.
        /// </summary>
        private VideoSearchDto BuildVideoSearch(string? externalMatchId, IReadOnlyList<MatchVideoDto> videos)
        {
            var ledger = string.IsNullOrWhiteSpace(externalMatchId)
                ? Services.PostMatch.FixtureAttemptSummary.None
                : _fixtureSync.GetFixtureAttemptSummary(externalMatchId!, FixtureRefreshPurposes.PostMatchVideo);

            var hasPlayable = videos.Any(v => v.CanPlayInApp);
            return new VideoSearchDto
            {
                Status = Services.PostMatch.PostMatchVideoSearchStatus.Resolve(hasPlayable, ledger),
                AttemptsMade = ledger.Attempts,
                MaxAttempts = Services.PostMatch.PostMatchVideoSchedule.MaxAttempts,
                LastAttemptUtc = ledger.LastAttemptUtc
            };
        }

        private LineupSectionDto BuildLineupSection(int matchId, DateTime kickoffUtc, string? externalMatchId)
        {
            var header  = _matchLineupRepository.GetByMatchId(matchId);
            var players = _matchLineupRepository.GetPlayersByMatchId(matchId);

            // BEKLEME DURUMU — arayüz sabit bir saat SÖZÜ vermesin diye taşınır.
            // Bu üç alan salt DB'den okunur; sayfa açılışı sağlayıcıya ÇIKMAZ.
            var nowUtc   = DateTime.UtcNow;
            var remaining = kickoffUtc - nowUtc;
            var ledger = string.IsNullOrWhiteSpace(externalMatchId)
                ? Services.PostMatch.FixtureAttemptSummary.None
                : _fixtureSync.GetFixtureAttemptSummary(externalMatchId!, FixtureRefreshPurposes.Lineup);

            var waitState = new
            {
                WindowOpen    = remaining <= Services.Matches.LineupPollSchedule.WindowOpen,
                KickoffPassed = remaining <= TimeSpan.Zero,
                // SON KONTROL: sağlayıcının GERÇEKTEN cevap verdiği son an — kural
                // LineupAvailability.LastRealCheck'te (engellenen rezervasyon sayılmaz).
                LastChecked   = Services.Matches.LineupAvailability.LastRealCheck(
                                    header?.LastCheckedAtUtc, header?.FetchedAt,
                                    ledger.LastAttemptUtc, ledger.LastOutcome)
            };

            if (header == null || players.Count == 0)
                return new LineupSectionDto
                {
                    LineupsAnnounced  = false,
                    PollingWindowOpen = waitState.WindowOpen,
                    KickoffPassed     = waitState.KickoffPassed,
                    LastCheckedUtc    = waitState.LastChecked,
                    Status            = Services.Matches.LineupAvailability.Resolve(false, kickoffUtc, nowUtc)
                };

            static LineupPlayerDto Map(MatchLineupPlayer p) => new()
            {
                ShirtNumber = p.ShirtNumber,
                PlayerName  = p.PlayerName,
                Position    = p.Position,
                Grid        = p.Grid,
                IsCaptain   = p.IsCaptain
            };

            return new LineupSectionDto
            {
                LineupsAnnounced = header.HomeLineupsReleased || header.AwayLineupsReleased,
                // Diziliş sağlayıcıdan gelir ve takım başına ayrıdır; yoksa null kalır.
                HomeFormation    = header.HomeFormation,
                AwayFormation    = header.AwayFormation,
                HomeStartingXI   = players.Where(p => p.Side == "Home" && p.Role == "Starter").OrderBy(p => p.ShirtNumber).Select(Map).ToList(),
                HomeBench        = players.Where(p => p.Side == "Home" && p.Role == "Bench")  .OrderBy(p => p.ShirtNumber).Select(Map).ToList(),
                AwayStartingXI   = players.Where(p => p.Side == "Away" && p.Role == "Starter").OrderBy(p => p.ShirtNumber).Select(Map).ToList(),
                AwayBench        = players.Where(p => p.Side == "Away" && p.Role == "Bench")  .OrderBy(p => p.ShirtNumber).Select(Map).ToList(),
                PollingWindowOpen = waitState.WindowOpen,
                KickoffPassed     = waitState.KickoffPassed,
                LastCheckedUtc    = waitState.LastChecked,
                // DB'de doğrulanmış kadro varsa maç başlamış/bitmiş olsa da gösterilir.
                Status            = Services.Matches.LineupAvailability.Resolve(
                                        header.HomeLineupsReleased || header.AwayLineupsReleased, kickoffUtc, nowUtc)
            };
        }

        private PlayerStatusSectionDto BuildPlayerStatusSection(int matchId)
        {
            var statuses = _matchPlayerStatusRepository.GetByMatchId(matchId);

            static PlayerStatusDto Map(MatchPlayerStatus s) => new()
            {
                PlayerName = s.PlayerName,
                TeamId     = s.TeamId,
                Status     = s.Status,
                Reason     = s.Reason
            };

            return new PlayerStatusSectionDto
            {
                Injuries    = statuses.Where(s => s.Status == "Injured")   .Select(Map).ToList(),
                Suspensions = statuses.Where(s => s.Status == "Suspended") .Select(Map).ToList(),
                Doubtful    = statuses.Where(s => s.Status == "Doubtful")  .Select(Map).ToList()
            };
        }

        // ────────────────────────────────────────────────────────────────────────
        // Sprint 2: Standings & competition context builders
        // ────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// PUAN DURUMU — ligin TAMAMI (kırpma yok).
        ///
        /// Önceki sürüm tabloyu "ilk 3 + iki takımın ±2 komşusu, en fazla 10 satır" olarak
        /// kırpıyordu; kullanıcı ligin gerçek sıralamasını göremiyordu. Artık ligdeki bütün
        /// takımlar Position ARTAN döner ve sıralama backend'in verdiğidir.
        ///
        /// Maçın kendi ligi için puan durumu olmayabilir (Avrupa kupaları/kupa maçlarının
        /// LeagueStandings satırı yoktur — ölçüldü: 2026 sezonunda yalnız ulusal ligler dolu).
        /// O durumda takımların KENDİ ulusal lig tabloları döner; her tabloda yalnız ilgili
        /// takım vurgulanır. Veri yoksa null — uydurma tablo üretilmez.
        /// </summary>
        /// <summary>
        /// İÇ KAYNAKLI PUAN DURUMU (öncelikli yol) — saatlik projeksiyonun snapshot'ı.
        ///
        /// Sağlayıcı tablosundan iki farkı vardır:
        ///  • Kimlik CANONICAL takım id'sidir → external/canonical karışması (Eyüpspor 3588 =
        ///    Fenerbahçe canonical) burada YAŞANMAZ; vurgulama doğrudan eşleşir.
        ///  • Kaynak kendi tamamlanmış maçlarımızdır; tazelik (CalculatedAtUtc) taşınır.
        ///
        /// Snapshot yoksa null döner ve çağıran sağlayıcı tablosuna düşer (geri-uyum).
        /// Bu metot HESAP TETİKLEMEZ: yalnız cache/DB okur.
        /// </summary>
        private StandingSectionDto? BuildStandingSectionFromSnapshot(Domain.Entities.Match match, int seasonYear)
        {
            var snapshot = _standingsService.GetCached(match.LeagueId, seasonYear);
            if (snapshot == null || snapshot.Rows.Count == 0) return null;

            TeamStandingDto Map(Formax.Application.DTOs.Standings.LeagueStandingsRowDto r) => new()
            {
                Position       = r.Position,
                TeamName       = r.TeamName,
                Played         = r.Played,
                Won            = r.Won,
                Drawn          = r.Drawn,
                Lost           = r.Lost,
                GoalsFor       = r.GoalsFor,
                GoalsAgainst   = r.GoalsAgainst,
                GoalDifference = r.GoalDifference,
                Points         = r.Points,
                Form           = r.Form,
                IsHighlighted  = r.TeamId == match.HomeTeamId || r.TeamId == match.AwayTeamId
            };

            var rows = snapshot.Rows.OrderBy(r => r.Position).Select(Map).ToList();

            var table = new StandingTableDto
            {
                LeagueId   = snapshot.LeagueId,
                LeagueName = string.IsNullOrWhiteSpace(snapshot.LeagueName)
                    ? ResolveLeagueName(match.LeagueId)
                    : snapshot.LeagueName,
                SeasonYear = snapshot.SeasonId,
                Rows       = rows
            };

            var homeRow = snapshot.Rows.FirstOrDefault(r => r.TeamId == match.HomeTeamId);
            var awayRow = snapshot.Rows.FirstOrDefault(r => r.TeamId == match.AwayTeamId);

            return new StandingSectionDto
            {
                LeagueId             = table.LeagueId,
                LeagueName           = table.LeagueName,
                SeasonYear           = snapshot.SeasonId,
                SeasonStartDate      = snapshot.SeasonStartDate,
                CalculatedAtUtc      = snapshot.CalculatedAtUtc,
                LastIncludedMatchUtc = snapshot.LastIncludedMatchUtc,
                Source               = snapshot.Source,
                IsFresh              = snapshot.IsFresh,
                IsProvisional        = snapshot.IsProvisional,
                ExpectedCompletedFixtures = snapshot.ExpectedCompletedFixtures,
                IncludedCompletedFixtures = snapshot.IncludedCompletedFixtures,
                MissingCompletedFixtures  = snapshot.MissingCompletedFixtures,
                IsComplete                = snapshot.IsComplete,
                CompletenessCheckedAtUtc  = snapshot.CompletenessCheckedAtUtc,
                PostponedFixtures         = snapshot.PostponedFixtures,
                CancelledFixtures         = snapshot.CancelledFixtures,
                AbandonedFixtures         = snapshot.AbandonedFixtures,
                StaleResultFixtures       = snapshot.StaleResultFixtures,
                RankingRuleId        = snapshot.RankingRuleId,
                MatchesIncluded      = snapshot.MatchesIncluded,
                HomeTeamPeek         = homeRow != null ? Map(homeRow) : null,
                AwayTeamPeek         = awayRow != null ? Map(awayRow) : null,
                TableSlice           = rows,
                Tables               = new List<StandingTableDto> { table }
            };
        }

        private StandingSectionDto? BuildStandingSection(Domain.Entities.Match match)
        {
            var seasonYear = ResolveSeasonYear(match.MatchDate);

            // ── AŞAMA KARARI (01.09.2026) ────────────────────────────────────────
            // UEFA maçlarında puan durumu her aşamada GÖSTERİLMEZ. Eleme maçında tablo
            // kavram olarak yoktur; aşama çözülemediyse tablo üretilmez. Karar merkezî
            // StandingsPresentation'da verilir, burada YENİDEN yorumlanmaz.
            var phase = Formax.Application.Services.Standings.CompetitionPhaseResolver
                .Resolve(match.LeagueId, match.Round);

            var internalSection = BuildStandingSectionFromSnapshot(match, seasonYear);
            var hasTable = internalSection != null && internalSection.TableSlice.Count > 0;

            var decision = Formax.Application.Services.Standings.StandingsPresentation
                .Decide(match.LeagueId, phase, hasTable);

            if (!decision.ShowTable)
            {
                // Tablo YOK ama kullanıcı sessiz bırakılmaz: nötr açıklama taşınır.
                // Sahte/boş tablo ÜRETİLMEZ.
                return new StandingSectionDto
                {
                    LeagueId              = match.LeagueId,
                    SeasonYear            = seasonYear,
                    MatchPhase            = Formax.Application.Services.Standings.StandingsPresentation.PhaseName(phase),
                    StandingsAvailability = decision.Availability,
                    StandingsTitle        = decision.Title,
                    StandingsNotice       = decision.Notice,
                    Diagnostic            = decision.Diagnostic
                };
            }

            if (internalSection != null)
            {
                internalSection.MatchPhase            = Formax.Application.Services.Standings.StandingsPresentation.PhaseName(phase);
                internalSection.StandingsAvailability = decision.Availability;
                internalSection.StandingsTitle        = decision.Title;
                internalSection.StandingsNotice       = decision.Notice;
                internalSection.Diagnostic            = decision.Diagnostic;
                return internalSection;
            }

            // 2) GERİ-UYUM: snapshot henüz üretilmediyse sağlayıcı tablosu (LeagueStandings).
            //    Bu yol YALNIZ ulusal ligler içindir: UEFA'da yukarıdaki karar zaten
            //    tablo göstermeyi reddetmiş olurdu.

            static TeamStandingDto Map(LeagueStanding s, bool highlight) => new()
            {
                Position       = s.Position,
                TeamName       = s.TeamName,
                Played         = s.Played,
                Won            = s.Won,
                Drawn          = s.Drawn,
                Lost           = s.Lost,
                GoalsFor       = s.GoalsFor,
                GoalsAgainst   = s.GoalsAgainst,
                GoalDifference = s.GoalDifference,
                Points         = s.Points,
                Form           = s.Form,
                IsHighlighted  = highlight
            };

            // KİMLİK EŞLEŞMESİ: LeagueStandings.TeamId sağlayıcının EXTERNAL takım id'sini tutar
            // (ör. Galatasaray = 645); maçın home/away'i ise CANONICAL id'dir (ör. 3588). Ölçüldü
            // (14.08): yalnız canonical id ile arandığı için puan durumu satırı hemen hiçbir maçta
            // bulunamıyordu (tablo dolu olsa bile Standing null dönüyordu). Aynı çözüm kadro
            // tarafında zaten uygulanıyor; burada da external id üzerinden eşlenir, canonical
            // fallback korunur. Yeni tablo/hesap yok.
            //
            // KİMLİK ÇAKIŞMASI (ölçüldü 18.08): canonical fallback KOŞULSUZ uygulanınca yanlış
            // takım vurgulanıyordu. İki kimlik uzayı örtüşür — Eyüpspor'un EXTERNAL id'si 3588,
            // Fenerbahçe'nin CANONICAL id'si de 3588'dir; Süper Lig tablosunda Fenerbahçe–Lyon
            // maçı için hem Fenerbahçe (ext 611) hem Eyüpspor (ext 3588) vurgulanıyordu.
            // Bu yüzden external id çözülebiliyorsa YALNIZ onunla eşleşilir; canonical fallback
            // ancak external yoksa devreye girer.
            var homeExt = ResolveTeamExternalId(match.HomeTeamId);
            var awayExt = ResolveTeamExternalId(match.AwayTeamId);

            bool IsHome(LeagueStanding s) =>
                homeExt.HasValue ? s.TeamId == homeExt.Value : s.TeamId == match.HomeTeamId;
            bool IsAway(LeagueStanding s) =>
                awayExt.HasValue ? s.TeamId == awayExt.Value : s.TeamId == match.AwayTeamId;

            // Gösterilecek lig(ler): önce maçın kendi ligi; yoksa takımların ulusal ligleri.
            var leagueIds = new List<int>();
            if (_leagueStandingRepository.GetByLeague(match.LeagueId, seasonYear).Count > 0)
            {
                leagueIds.Add(match.LeagueId);
            }
            else
            {
                var homeLeague = ResolveTeamLeagueId(match.HomeTeamId, homeExt, seasonYear);
                var awayLeague = ResolveTeamLeagueId(match.AwayTeamId, awayExt, seasonYear);
                if (homeLeague.HasValue) leagueIds.Add(homeLeague.Value);
                if (awayLeague.HasValue && awayLeague != homeLeague) leagueIds.Add(awayLeague.Value);
            }

            var tables = new List<StandingTableDto>();
            LeagueStanding? homePeek = null;
            LeagueStanding? awayPeek = null;

            foreach (var leagueId in leagueIds)
            {
                var rows = _leagueStandingRepository.GetByLeague(leagueId, seasonYear);
                if (rows.Count == 0) continue;

                homePeek ??= rows.FirstOrDefault(IsHome);
                awayPeek ??= rows.FirstOrDefault(IsAway);

                tables.Add(new StandingTableDto
                {
                    LeagueId   = leagueId,
                    LeagueName = ResolveLeagueName(leagueId),
                    SeasonYear = seasonYear,
                    Rows = rows
                        .OrderBy(s => s.Position)
                        .Select(s => Map(s, IsHome(s) || IsAway(s)))
                        .ToList()
                });
            }

            if (tables.Count == 0) return null;

            var primary = tables[0];

            return new StandingSectionDto
            {
                LeagueId      = primary.LeagueId,
                LeagueName    = primary.LeagueName,
                SeasonYear    = seasonYear,
                HomeTeamPeek  = homePeek != null ? Map(homePeek, true) : null,
                AwayTeamPeek  = awayPeek != null ? Map(awayPeek, true) : null,
                // Geri-uyum: eski tüketiciler bu alanı okur. Artık kırpılmamış birincil tablo.
                TableSlice    = primary.Rows,
                Tables        = tables
            };
        }

        /// <summary>
        /// Takımın ULUSAL LİG kimliği — puan durumu tablosu yalnız ligler için yazıldığından
        /// gerçek veriye dayalı tek kaynak LeagueStandings'tir. İsimden tahmin yapılmaz.
        /// </summary>
        private int? ResolveTeamLeagueId(int canonicalTeamId, int? externalTeamId, int seasonYear)
        {
            // Kimlik uzayları örtüştüğü için (bkz. BuildStandingSection'daki 3588 örneği)
            // external id varsa YALNIZ o kullanılır; canonical yalnız fallback'tir.
            var candidates = externalTeamId.HasValue
                ? new[] { externalTeamId.Value }
                : new[] { canonicalTeamId };
            return _leagueStandingRepository.FindLeagueIdForTeam(seasonYear, candidates);
        }

        /// <summary>
        /// Lig id → ligin GERÇEK adı (Matches.League). Ad üretilmez; kayıt yoksa boş döner.
        /// </summary>
        private string ResolveLeagueName(int leagueId)
            => _matchReadRepository.Query()
                   .Where(m => m.LeagueId == leagueId && m.League != null && m.League != "")
                   .Select(m => m.League)
                   .FirstOrDefault() ?? string.Empty;

        /// <summary>
        /// Canonical takım → sağlayıcının external takım id'si. Puan durumu ve kadro tabloları
        /// external id ile yazıldığı için eşleşme bunun üzerinden kurulur. Çözülemezse null.
        /// </summary>
        private int? ResolveTeamExternalId(int canonicalTeamId)
        {
            var team = _teamReadRepository.GetById(canonicalTeamId);
            return int.TryParse(team?.ExternalTeamId, out var ext) ? ext : (int?)null;
        }

        private CompetitionContextSectionDto? BuildCompetitionContextSection(int matchId)
        {
            var ctx = _competitionContextRepository.GetByMatchId(matchId);
            if (ctx == null) return null;

            return new CompetitionContextSectionDto
            {
                CompetitionType  = ctx.CompetitionType,
                StageName        = ctx.StageName,
                ContextHeadline  = ctx.ContextHeadline,
                ContextSummary   = ctx.ContextSummary,
                BracketJson      = ctx.BracketJson
            };
        }

        // ────────────────────────────────────────────────────────────────────────
        // Sprint 3: Live section builder
        // ────────────────────────────────────────────────────────────────────────

        private LiveSectionDto BuildLiveSection(Domain.Entities.Match match)
        {
            var statsEntity = _matchLiveStatsRepository.GetByMatchId(match.Id);
            LiveStatsDto? stats = null;

            if (statsEntity != null)
            {
                stats = new LiveStatsDto
                {
                    HomeScore          = statsEntity.HomeScore,
                    AwayScore          = statsEntity.AwayScore,
                    Minute             = statsEntity.Minute,
                    Phase              = statsEntity.Phase,
                    PossessionHome     = statsEntity.PossessionHome,
                    PossessionAway     = statsEntity.PossessionAway,
                    ShotsHome          = statsEntity.ShotsHome,
                    ShotsAway          = statsEntity.ShotsAway,
                    ShotsOnTargetHome  = statsEntity.ShotsOnTargetHome,
                    ShotsOnTargetAway  = statsEntity.ShotsOnTargetAway,
                    CornersHome        = statsEntity.CornersHome,
                    CornersAway        = statsEntity.CornersAway,
                    FoulsHome          = statsEntity.FoulsHome,
                    FoulsAway          = statsEntity.FoulsAway,
                    OffsidesHome       = statsEntity.OffsidesHome,
                    OffsidesAway       = statsEntity.OffsidesAway,
                    YellowHome         = statsEntity.YellowHome,
                    YellowAway         = statsEntity.YellowAway,
                    RedHome            = statsEntity.RedHome,
                    RedAway            = statsEntity.RedAway,
                    DangerousAttacksHome = statsEntity.DangerousAttacksHome,
                    DangerousAttacksAway = statsEntity.DangerousAttacksAway,
                    XgHome             = statsEntity.XgHome,
                    XgAway             = statsEntity.XgAway,
                    UpdatedAt          = statsEntity.UpdatedAt
                };
            }

            var events   = _matchLiveEventRepository.GetByMatchId(match.Id);
            var timeline = events.Select(e => new LiveEventDto
            {
                Minute      = e.Minute,
                EventType   = e.EventType,
                TeamName    = e.Team   ?? string.Empty,
                PlayerName  = e.Player ?? string.Empty,
                Detail      = e.Detail ?? string.Empty,
                ImpactScore = e.ImpactScore
            }).ToList();

            var snapshots = _matchMomentumRepository.GetByMatchId(match.Id, maxCount: 90);
            var momentum  = snapshots.Select(s => new MomentumSnapshotDto
            {
                Minute       = s.MinuteBucket,
                HomePressure = s.HomePressure,
                AwayPressure = s.AwayPressure
            }).ToList();

            return new LiveSectionDto { Stats = stats, Timeline = timeline, Momentum = momentum };
        }

        // ────────────────────────────────────────────────────────────────────────
        // Sprint 4: NABIZ section builder
        // ────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// SON DAKİKA bölümünün gerçek kaynağı.
        ///
        /// NEDEN VAR (ölçüldü 18.08.2026): ekran <see cref="BuildNabizSection"/> üzerinden
        /// MatchSocialFeedItems tablosunu okuyordu; NABIZ hattı `Nabiz:Sources` boş olduğu için
        /// HİÇ veri üretmiyor ve her maçta "güncel haber bulunmuyor" görünüyordu. Maçın gerçek
        /// haberleri Data Engine v2'nin MatchNewsArticles tablosunda duruyor (ölçüm: 18.839
        /// haber / 486 maç) ve zaten FORMAX_MATCH_ID altında, maç kapsam kapılarından geçmiş
        /// hâlde saklanıyor.
        ///
        /// Yeni haber sistemi/polling/provider YOKTUR: var olan depo okunur, var olan DTO
        /// (<see cref="NabizSectionDto"/>) doldurulur. NABIZ hattı ileride veri üretirse onun
        /// içeriği korunur (bu yol yalnız boşken devreye girer).
        ///
        /// SIRALAMA: en yeni önce (PublishedUtc DESC) — frontend yeniden sıralamaz.
        /// TEKİLLEŞTİRME: yazarken ContentHash benzersizdir; okurken ek olarak kanonik URL
        /// üzerinden de ayıklanır (aynı hikâye farklı hash'le iki kez yazılmışsa).
        /// GÖRSEL: haber hattı görsel URL'si TAŞIMIYOR (ne DedupedNewsItem'da ne tabloda alan
        /// var) → ImageUrl null bırakılır; görsel UYDURULMAZ.
        ///
        /// KAPSAM: MatchNewsArticles bilerek FİLTRESİZ keşif katmanıdır (NewsDiscoveryJob:
        /// "Ham haber KEŞİF katmanında saklanmaya devam eder"). Ölçüldü (18.08): Fenerbahçe–
        /// Konyaspor maçının ham listesinin ilk 10 başlığı Fenerbahçe–LYON maçınındı. Bu yüzden
        /// okuma yolunda MEVCUT kapılar uygulanır — <see cref="MatchIntelligenceService"/>
        /// içindeki public static süzgeçler; yeni filtre/sistem YAZILMAZ.
        ///
        /// KAYNAK KALİTESİ KAPISI UYGULANMAZ: o kapı (MinEvidenceSourceQuality=85) AI'ın
        /// "gerçek kabul ettiği" kanıt katmanı içindir. Son Dakika bir HABER listesidir;
        /// Tier-3 yayıncı haberi kullanıcıdan gizlenmez, yalnız yanlış maça ait ve bilgi
        /// taşımayan içerik elenir.
        /// </summary>
        private async Task<NabizSectionDto> BuildNewsSectionAsync(
            string formaxMatchId, MatchDetailDto detail, CancellationToken ct)
        {
            // NABIZ gerçekten veri ürettiyse ona dokunma (geri-uyum).
            if (detail.NabizFeed.Items.Count > 0) return detail.NabizFeed;

            // Liste üretimi TEK yerde: MatchNewsFeedService. Dile duyarlı /news ucu da aynı
            // servisi kullanır → aynı maç için iki farklı haber listesi oluşamaz.
            var items = await _newsFeedService.BuildAsync(
                formaxMatchId, detail.HomeTeam.Name, detail.AwayTeam.Name, detail.MatchDate, ct);

            // NOT: /detail ÇEVİRİ YAPMAZ. Çeviri LLM çağrısıdır; maç detayının tamamını
            // bekletmemek için yalnız dile duyarlı /news ucunda uygulanır.
            return items.Count == 0 ? detail.NabizFeed : new NabizSectionDto { Items = items };
        }

        private NabizSectionDto BuildNabizSection(int matchId)
        {
            var raw = _nabizFeedRepository.GetForMatch(matchId, limit: 20);

            var items = raw.Select(x => new NabizFeedItemDto
            {
                Type           = x.SourceType.ToString(),
                Source         = x.Source,
                Author         = x.Author,
                AuthorVerified = x.AuthorVerified,
                Headline       = x.Headline,
                Summary        = string.IsNullOrWhiteSpace(x.Summary) ? null : x.Summary,
                ImageUrl       = x.ImageUrl,
                SourceUrl      = x.SourceUrl,
                PublishedAt    = x.PublishedAt
            }).ToList();

            return new NabizSectionDto { Items = items };
        }

        // ────────────────────────────────────────────────────────────────────────
        // Utilities
        // ────────────────────────────────────────────────────────────────────────

        private static int ResolveSeasonYear(DateTime matchDate)
            => matchDate.Month >= 7 ? matchDate.Year : matchDate.Year - 1;
    }
}
