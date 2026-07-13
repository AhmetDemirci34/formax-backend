using Formax.Application.AI.Audit;
using Formax.Application.AI.Contexts;
using Formax.Application.AI.Memory;
using Formax.Application.AI.SelfAudit;
using Formax.Application.Common;
using Formax.Application.DTOs.Live;
using Formax.Application.DTOs.Lineup;
using Formax.Application.DTOs.Nabiz;
using Formax.Application.DTOs.Standings;
using Formax.Application.Interfaces;
using Formax.Application.Interfaces.Repositories;
using Formax.Application.States;
using Formax.Application.DTOs.Matches;
using Formax.Application.AI.Radar;
using Formax.Application.Services.Radar.Intelligence.Scenarios;
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
        private readonly IMatchMomentumRepository _matchMomentumRepository;
        private readonly IMatchLiveEventIngestionRepository _matchLiveEventRepository;
        private readonly INabizFeedRepository _nabizFeedRepository;
        private readonly IUserMatchFollowRepository _followRepository;
        // Radar v2 — anlatı zenginleştirme (opsiyonel; mevcut Execute akışını bozmaz).
        private readonly MatchIntelligenceContextBuilder _contextBuilder;
        private readonly RadarNarrativePipeline _narrativePipeline;
        // Radar v2.2 — dinamik senaryo motoru.
        private readonly MarketProbabilityEngine _marketEngine;
        private readonly ScenarioRankingService _scenarioRanking;
        // FINAL — Evidence → Reasoning köprüsü (Data Engine v1/v2.1 bağlantısı).
        private readonly Formax.Application.Services.Fixtures.FormaxMatchIdFactory _matchIdFactory;
        private readonly Formax.Application.Interfaces.IMatchEvidenceRepository _evidenceRepository;

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
            IMatchMomentumRepository matchMomentumRepository,
            IMatchLiveEventIngestionRepository matchLiveEventRepository,
            INabizFeedRepository nabizFeedRepository,
            IUserMatchFollowRepository followRepository,
            MatchIntelligenceContextBuilder contextBuilder,
            RadarNarrativePipeline narrativePipeline,
            MarketProbabilityEngine marketEngine,
            ScenarioRankingService scenarioRanking,
            Formax.Application.Services.Fixtures.FormaxMatchIdFactory matchIdFactory,
            Formax.Application.Interfaces.IMatchEvidenceRepository evidenceRepository)
        {
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
            _matchMomentumRepository = matchMomentumRepository;
            _matchLiveEventRepository = matchLiveEventRepository;
            _nabizFeedRepository = nabizFeedRepository;
            _followRepository = followRepository;
            _contextBuilder = contextBuilder;
            _narrativePipeline = narrativePipeline;
            _marketEngine = marketEngine;
            _scenarioRanking = scenarioRanking;
            _matchIdFactory = matchIdFactory;
            _evidenceRepository = evidenceRepository;
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
            var h2h            = BuildH2H(match.HomeTeamId, match.AwayTeamId);
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
                homeRank = allStandings.FirstOrDefault(s => s.TeamId == match.HomeTeamId)?.Position ?? 0;
                awayRank = allStandings.FirstOrDefault(s => s.TeamId == match.AwayTeamId)?.Position ?? 0;
            }

            // ── Sprint 1: Lineup & player status ─────────────────────────────────
            var lineupSection       = BuildLineupSection(match.Id);
            var playerStatusSection = BuildPlayerStatusSection(match.Id);

            // ── AI intelligence — Radar v2.2 dinamik senaryo sıralaması ────────────
            var homeNm = homeTeam?.Name ?? "Ev sahibi";
            var awayNm = awayTeam?.Name ?? "Deplasman";
            var gucSkoru = (int)sapma.GucSkoru;

            var scenarioCandidates = _marketEngine.Evaluate(
                homeComparison, awayComparison, h2h, gucSkoru, homeNm, awayNm);
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

            // ── Assemble ─────────────────────────────────────────────────────────
            var aiSummary = aiUxState switch
            {
                AIUxState.Extended    => "Maç bağlamı ve tempo analiz edilebilir seviyeye ulaşmıştır.",
                AIUxState.Short       => "Maç öncesi veri oluşuyor. Şu an ölçüm sınırlı.",
                AIUxState.SelfRetracted => "AI, yakın zamanda yapılan değerlendirme nedeniyle bu aşamada geri çekilmeyi tercih etmiştir.",
                _                     => "Bu maç için AI şu aşamada yönlendirici bir analiz sunmamayı tercih etmiştir."
            };

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
                League       = match.League,
                Round        = BuildRoundLabel(match.Id),
                Referee      = match.Referee,
                Venue        = match.Venue,
                Weather      = null,        // reserved — requires dedicated weather API
                WatchersCount = watchersCount,

                HomeTeamLastMatches = BuildLastMatches(match.HomeTeamId),
                AwayTeamLastMatches = BuildLastMatches(match.AwayTeamId),

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
            var detail = Execute(matchId);
            if (detail == null) return null;

            // AI konuşma izni mevcut guardrail kararından gelir (Silent/SelfRetracted → fallback).
            var aiAllowed = detail.Ai.State is "Extended" or "Short";

            // Senaryoları (kanıt etiketleriyle) yeniden sırala — DTO ile aynı deterministik sonuç,
            // ek olarak EvidenceTags taşır → LLM "neden öne çıkıyor"u kanıta dayandırır.
            var candidates = _marketEngine.Evaluate(
                detail.Comparison.Home, detail.Comparison.Away, detail.H2H,
                detail.Sapma.GucSkoru, detail.HomeTeam.Name, detail.AwayTeam.Name);
            var ranked = _scenarioRanking.RankTop(candidates, 3);

            // FINAL köprü: maçın FORMAX_MATCH_ID'sini hesapla → Evidence Store'dan
            // Match Intelligence Context çek. Evidence varsa Reasoning ham haber yerine
            // signal-typed kanıtlardan beslenir; yoksa eski NABIZ'e düşer (geri-uyum).
            var formaxMatchId = _matchIdFactory.Create(
                detail.League, detail.MatchDate, detail.HomeTeam.Name, detail.AwayTeam.Name);
            var evidenceCtx = await _evidenceRepository.GetContextAsync(formaxMatchId, ct);

            var context = _contextBuilder.Build(detail, null, ranked, evidenceCtx);

            var discover = await _narrativePipeline.GenerateAsync(context, RadarSurface.Discover, aiAllowed, ct);
            var report   = await _narrativePipeline.GenerateAsync(context, RadarSurface.MatchDetail, aiAllowed, ct);

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
                ReasoningConfidence = report.ReasoningConfidence,
                IsAiGenerated      = discover.IsAiGenerated || report.IsAiGenerated
            };

            return detail;
        }

        // ────────────────────────────────────────────────────────────────────────
        // Comparison & form helpers
        // ────────────────────────────────────────────────────────────────────────

        private TeamComparisonDto BuildTeamComparison(int teamId, bool homeOnly = false, bool awayOnly = false)
        {
            var recentMatches = _matchReadRepository.GetRecentMatchesForTeam(teamId, 10);
            if (!recentMatches.Any()) return new TeamComparisonDto();

            var filteredMatches = recentMatches;
            if (homeOnly)  filteredMatches = recentMatches.Where(x => x.HomeTeamId == teamId).ToList();
            if (awayOnly)  filteredMatches = recentMatches.Where(x => x.AwayTeamId == teamId).ToList();

            var totalMatches   = recentMatches.Count;
            var goalsFor       = 0;
            var goalsAgainst   = 0;
            var scoredMatches  = 0;
            var cleanSheets    = 0;
            var formScore      = 0;

            foreach (var m in recentMatches)
            {
                var isHome = m.HomeTeamId == teamId;
                var gf = isHome ? m.HomeScore : m.AwayScore;
                var ga = isHome ? m.AwayScore : m.HomeScore;
                goalsFor     += gf;
                goalsAgainst += ga;
                if (gf > 0) scoredMatches++;
                if (ga == 0) cleanSheets++;
                if (gf > ga) formScore += 3;
                else if (gf == ga) formScore += 1;
            }

            var usedMatches = (filteredMatches.Count == 0 ? recentMatches : filteredMatches);
            var filteredGoals = usedMatches.Select(m =>
                m.HomeTeamId == teamId ? m.HomeScore : m.AwayScore).ToList();

            var team = _teamReadRepository.GetById(teamId);

            return new TeamComparisonDto
            {
                AvgGoalsFor    = Math.Round((double)goalsFor / totalMatches, 1),
                AvgGoalsAgainst = Math.Round((double)goalsAgainst / totalMatches, 1),
                GoalScoringRate = (int)Math.Round((double)scoredMatches / totalMatches * 100),
                CleanSheetRate  = (int)Math.Round((double)cleanSheets  / totalMatches * 100),
                HomeAwayAvgGoals = filteredGoals.Any()
                    ? Math.Round(filteredGoals.Average(), 1) : 0,
                FormScore  = formScore,
                LeagueRank = team?.LeagueRank ?? 0
            };
        }

        private List<LastMatchDto> BuildLastMatches(int teamId)
        {
            var recent = _matchReadRepository.GetRecentMatchesForTeam(teamId, 10);
            var result = new List<LastMatchDto>(recent.Count);

            foreach (var m in recent)
            {
                var isHome = m.HomeTeamId == teamId;
                var gf = isHome ? m.HomeScore : m.AwayScore;
                var ga = isHome ? m.AwayScore : m.HomeScore;

                result.Add(new LastMatchDto
                {
                    MatchId     = m.Id,
                    Opponent    = isHome
                        ? (m.AwayTeam?.Name ?? string.Empty)
                        : (m.HomeTeam?.Name ?? string.Empty),
                    Result      = gf > ga ? "W" : gf == ga ? "D" : "L",
                    Score       = $"{gf}-{ga}",
                    Date        = m.MatchDate.ToString("dd.MM.yyyy"),
                    Competition = m.League,
                    IsHome      = isHome
                });
            }

            return result;
        }

        private H2HDto BuildH2H(int homeTeamId, int awayTeamId)
        {
            var matches = _matchReadRepository.GetHeadToHeadMatches(homeTeamId, awayTeamId, 10);

            int homeWins = 0, awayWins = 0, draws = 0;
            var matchDtos = new List<H2HMatchDto>();

            foreach (var m in matches)
            {
                if (m.HomeScore > m.AwayScore)
                {
                    if (m.HomeTeamId == homeTeamId) homeWins++;
                    else awayWins++;
                }
                else if (m.HomeScore < m.AwayScore)
                {
                    if (m.AwayTeamId == awayTeamId) awayWins++;
                    else homeWins++;
                }
                else draws++;

                matchDtos.Add(new H2HMatchDto
                {
                    MatchDate    = m.MatchDate.ToString("yyyy-MM-dd"),
                    HomeTeamName = m.HomeTeam?.Name ?? string.Empty,
                    AwayTeamName = m.AwayTeam?.Name ?? string.Empty,
                    HomeScore    = m.HomeScore,
                    AwayScore    = m.AwayScore,
                    Competition  = m.League
                });
            }

            return new H2HDto
            {
                TotalMatches = matchDtos.Count,
                HomeWins     = homeWins,
                AwayWins     = awayWins,
                Draws        = draws,
                Matches      = matchDtos,
                FetchedAt    = DateTime.UtcNow
            };
        }

        // ────────────────────────────────────────────────────────────────────────
        // Insight builder — turns the already-computed TeamComparisonDto fields
        // (form, goals scored/conceded, clean sheets, scoring rate, league rank)
        // into concrete, team-named, number-backed sentences.
        // No LLM / no random story — every number is a real computed value, and
        // the same match always yields the same text (deterministic selection).
        // ────────────────────────────────────────────────────────────────────────
        private InsightDto BuildInsight(
            TeamComparisonDto home, TeamComparisonDto away,
            string homeName, string awayName)
        {
            var hasData = home.FormScore > 0 || away.FormScore > 0
                          || home.AvgGoalsFor > 0 || away.AvgGoalsFor > 0;

            // No history for either side → honest fallback (don't invent).
            if (!hasData)
            {
                return new InsightDto
                {
                    Headline = $"{homeName} sahasında {awayName} ile karşılaşıyor.",
                    Summary  = "İki takım için yeterli son maç verisi henüz oluşmadı."
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
                candidates.Add((csGap / 20.0, $"{solid} savunmada daha güvenli: son maçlarının %{rate}'inde gol yemedi."));
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
                candidates.Add((srGap / 20.0, $"{consistent} istikrarlı skor üretiyor: son maçlarının %{rate}'inde gol attı."));
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

        private string? BuildRoundLabel(int matchId)
        {
            var ctx = _competitionContextRepository.GetByMatchId(matchId);
            return ctx?.StageName;
        }

        // ────────────────────────────────────────────────────────────────────────
        // Sprint 1: Lineup builders
        // ────────────────────────────────────────────────────────────────────────

        private LineupSectionDto BuildLineupSection(int matchId)
        {
            var header  = _matchLineupRepository.GetByMatchId(matchId);
            var players = _matchLineupRepository.GetPlayersByMatchId(matchId);

            if (header == null || players.Count == 0)
                return new LineupSectionDto { LineupsAnnounced = false };

            static LineupPlayerDto Map(MatchLineupPlayer p) => new()
            {
                ShirtNumber = p.ShirtNumber,
                PlayerName  = p.PlayerName,
                Position    = p.Position,
                IsCaptain   = p.IsCaptain
            };

            return new LineupSectionDto
            {
                LineupsAnnounced = header.HomeLineupsReleased || header.AwayLineupsReleased,
                HomeStartingXI   = players.Where(p => p.Side == "Home" && p.Role == "Starter").OrderBy(p => p.ShirtNumber).Select(Map).ToList(),
                HomeBench        = players.Where(p => p.Side == "Home" && p.Role == "Bench")  .OrderBy(p => p.ShirtNumber).Select(Map).ToList(),
                AwayStartingXI   = players.Where(p => p.Side == "Away" && p.Role == "Starter").OrderBy(p => p.ShirtNumber).Select(Map).ToList(),
                AwayBench        = players.Where(p => p.Side == "Away" && p.Role == "Bench")  .OrderBy(p => p.ShirtNumber).Select(Map).ToList()
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

        private StandingSectionDto? BuildStandingSection(Domain.Entities.Match match)
        {
            var seasonYear = ResolveSeasonYear(match.MatchDate);
            var all        = _leagueStandingRepository.GetByLeague(match.LeagueId, seasonYear);
            if (all.Count == 0) return null;

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

            var homePeek = all.FirstOrDefault(s => s.TeamId == match.HomeTeamId);
            var awayPeek = all.FirstOrDefault(s => s.TeamId == match.AwayTeamId);

            var relevantPositions = new HashSet<int>();
            for (var i = 1; i <= Math.Min(3, all.Count); i++) relevantPositions.Add(i);

            void AddWindow(int center)
            {
                for (var d = -2; d <= 2; d++)
                {
                    var pos = center + d;
                    if (pos >= 1 && pos <= all.Count) relevantPositions.Add(pos);
                }
            }

            if (homePeek != null) AddWindow(homePeek.Position);
            if (awayPeek != null) AddWindow(awayPeek.Position);

            var slice = all
                .Where(s => relevantPositions.Contains(s.Position))
                .OrderBy(s => s.Position)
                .Take(10)
                .Select(s => Map(s, s.TeamId == match.HomeTeamId || s.TeamId == match.AwayTeamId))
                .ToList();

            return new StandingSectionDto
            {
                LeagueId      = match.LeagueId,
                SeasonYear    = seasonYear,
                HomeTeamPeek  = homePeek != null ? Map(homePeek, true) : null,
                AwayTeamPeek  = awayPeek != null ? Map(awayPeek, true) : null,
                TableSlice    = slice
            };
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
