using System.Text.Json;
using Formax.Application.DTOs.Recommendations;
using Formax.Application.Interfaces;
using Formax.Application.Interfaces.Repositories;
using Formax.Application.UseCases.Home;
using Formax.Application.DTOs.Home;
using Formax.Application.Services.Radar.Feed;
using Formax.Application.Services.Radar.Intelligence.Match;

public sealed class GetRecommendationFeedUseCase
{
    private const double TEAM_FOLLOW_BOOST = 0.15;

    // ── Phase 7: cross-user global trend boost ──────────────────────────────
    private const double GLOBAL_BOOST_CAP        = 0.20;  // max contribution (pre-sigmoid)
    private const double GLOBAL_K                = 0.40;  // slope on (globalScore - 0.5)
    private const double HIGH_INTEREST_THRESHOLD = 0.10;  // personal-interest priority gate
    private const int    MIN_DISTINCT_USERS      = 3;     // single-user is not a "global trend"

    // Distinct-user guard intentionally DISABLED in dev (single seed user only).
    // Flip to true once a real multi-user base exists (beta) to enforce the gate.
    private const bool   ENABLE_DISTINCT_USER_GUARD = false;

    private readonly GetHomeRadarUseCase _radarUseCase;
    private readonly IRecommendationEngine _engine;
    private readonly IUserRepository _userRepository;
    private readonly IUserActionRepository _actionRepository;
    private readonly IUserPreferenceRepository _prefRepo;
    private readonly IMatchBanditRepository _banditRepo;
    private readonly IMatchReadRepository _matchRepo;
    private readonly IUserTeamFollowRepository _teamFollowRepo;
    private readonly IFeedInsightQueryService _feedInsightQuery;
    private readonly IRadarFeedAdapter _radarFeedAdapter;
    private readonly IRadarRankingService _radarRanking;
    private readonly IMatchIntelligenceRepository _intelRepo;

    private static readonly JsonSerializerOptions _signalJsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    public GetRecommendationFeedUseCase(
        GetHomeRadarUseCase radarUseCase,
        IRecommendationEngine engine,
        IUserRepository userRepository,
        IUserActionRepository actionRepository,
        IUserPreferenceRepository prefRepo,
        IMatchBanditRepository banditRepo,
        IMatchReadRepository matchRepo,
        IUserTeamFollowRepository teamFollowRepo,
        IFeedInsightQueryService feedInsightQuery,
        IRadarFeedAdapter radarFeedAdapter,
        IRadarRankingService radarRanking,
        IMatchIntelligenceRepository intelRepo)
    {
        _radarUseCase = radarUseCase;
        _engine = engine;
        _userRepository = userRepository;
        _actionRepository = actionRepository;
        _prefRepo = prefRepo;
        _banditRepo = banditRepo;
        _matchRepo = matchRepo;
        _teamFollowRepo = teamFollowRepo;
        _feedInsightQuery = feedInsightQuery;
        _radarFeedAdapter = radarFeedAdapter;
        _radarRanking = radarRanking;
        _intelRepo = intelRepo;
    }

    public async Task<List<RecommendationCardDto>> Execute(int userId, int page = 1, int pageSize = 10)
    {
        var matches = _matchRepo.Query()
            .OrderByDescending(x => x.MatchDate)
            .Take(100)
            .ToList();

        var matchById = matches.ToDictionary(m => m.Id);

        var followedTeamIds = (await _teamFollowRepo.GetActiveByUserAsync(userId))
            .Select(x => x.TeamId)
            .ToHashSet();

        // ── Phase 7: distinct-user guard. A single user's repeated actions
        // must not count as a "global trend" (audit rule 4).
        // DEV: guard disabled → layer active for testing with the single seed user.
        // PROD: flip ENABLE_DISTINCT_USER_GUARD = true. When enabled, a real
        // distinct-user count source is required (e.g. an IUserActionRepository
        // CountDistinctUsersAsync) — until that exists the guard stays closed,
        // keeping the layer off rather than trusting a single-user signal.
        var globalLayerEnabled = !ENABLE_DISTINCT_USER_GUARD;

        // 🔥 ENTITY → DTO MAP
        var radarMatches = matches.Select(m => new HomeRadarMatchDto
        {
            MatchId = m.Id,

            Teams = new HomeTeamsDto
            {
                Home = m.HomeTeam.Name,
                Away = m.AwayTeam.Name
            },

            League = m.League,
            LeagueName = m.League,

            // bunlar sende yoksa 0 ver (engine zaten normalize eder)
            RadarScore = 0,
            TeamInterestScore = 0,
            LeagueInterestScore = 0,
            ContentInterestScore = 0,
            BehaviorMomentumScore = 0,
            MatchHeatScore = 0,
            LeagueBaselineScore = 0,
            TimeProximityScore = 0,

            Freshness = "NEW",
            Explainability = "",
            Reasons = new List<string>(),

            PlayRate = 0,
            TrendDelta = 0,
            OddsMovementScore = 0,
            ViewDurationMs = 0,
            UserSwipeScore = 0,
            OpenedDetail = false,
            Followed = false
        }).ToList();

        // 🔥 ARTIK DOĞRU TİP
        var result = await _engine.BuildRecommendationFeed(userId, radarMatches);

        var actions = await _actionRepository.GetByUserIdAsync(userId);
        var weights = await _prefRepo.GetOrCreate(userId);

        foreach (var x in result)
        {
            var baseScore =
                (0.5 * x.UserTrendScore) +
                (0.3 * x.MarketTrendScore) +
                (0.2 * x.MomentumScore);

            double decay = 1.0;
            if (actions.Count > 0)
            {
                var last = actions.First().CreatedAt;
                var days = (DateTime.UtcNow - last).TotalDays;
                decay = Math.Exp(-0.1 * days);
            }

            double userBoost = 0;

            var sameMatch = actions.FirstOrDefault(a => a.MatchId == x.MatchId);

            if (sameMatch != null)
            {
                if (sameMatch.ActionType == 1) userBoost += weights.LikeWeight;
                if (sameMatch.ActionType == -1) userBoost += weights.SkipWeight;
            }

            var teamLikes = actions.Count(a =>
                a.ActionType == 1 &&
                (a.Team == x.TeamA || a.Team == x.TeamB));

            var teamSkips = actions.Count(a =>
                a.ActionType == -1 &&
                (a.Team == x.TeamA || a.Team == x.TeamB));

            userBoost += Math.Min(0.3, teamLikes * weights.TeamWeight);
            userBoost -= Math.Min(0.3, teamSkips * weights.TeamWeight);

            userBoost = Math.Max(-0.4, Math.Min(0.4, userBoost));
            userBoost *= decay;

            var stats = await _banditRepo.GetOrCreate(x.MatchId);

            double ucb = 0;

            if (stats.Impressions > 0)
            {
                var ctr = (double)stats.Likes / stats.Impressions;
                var exploration =
                    Math.Sqrt(2 * Math.Log(actions.Count + 1) / stats.Impressions);

                ucb = Math.Min(1.0, ctr + exploration);
            }

            var rawScore = baseScore + userBoost + (ucb * 0.03);

            matchById.TryGetValue(x.MatchId, out var matchEntity);

            // League rank — importance signal only, does not affect scoring.
            x.HomeRank = matchEntity?.HomeTeam?.LeagueRank;
            x.AwayRank = matchEntity?.AwayTeam?.LeagueRank;

            var followsTeam =
                followedTeamIds.Count > 0
                && matchEntity != null
                && (followedTeamIds.Contains(matchEntity.HomeTeamId)
                    || followedTeamIds.Contains(matchEntity.AwayTeamId));

            if (followsTeam)
                rawScore += TEAM_FOLLOW_BOOST;

            // ── Phase 7: cross-user global trend boost (capped, personal-priority) ──
            if (globalLayerEnabled)
            {
                var globalBoost = Math.Clamp(
                    (x.GlobalTrendScore - 0.5) * GLOBAL_K, 0.0, GLOBAL_BOOST_CAP);

                // Personal interest always wins: dampen global when the user
                // follows a team or already shows strong interest in this card.
                if (followsTeam || userBoost >= HIGH_INTEREST_THRESHOLD)
                    globalBoost *= 0.3;

                rawScore += globalBoost;
            }

            x.RecommendationScore = rawScore / (1 + rawScore);

            x.ConfidenceScore = Math.Min(1.0,
                (x.RecommendationScore * 0.7) +
                (x.GlobalTrendScore * 0.3)
            );

            x.ConfidenceLabel = x.ConfidenceScore switch
            {
                > 0.75 => "HIGH",
                > 0.50 => "MEDIUM",
                _ => "LOW"
            };

            x.AiSummary = "";

            x.PersonalReason =
                $"Boost:{Math.Round(userBoost, 2)} UCB:{Math.Round(ucb, 2)}";

            var matchIsHot = x.DirectionScore > 0;

            x.StoryHeadline = BuildStoryHeadline(
                x.TeamA, x.TeamB,
                x.HomeRank, x.AwayRank,
                followsTeam, matchIsHot,
                matchEntity?.League);

            x.StoryBody = BuildStoryBody(
                x.StoryHeadline, x.TeamA, x.TeamB);

            x.Tags = BuildMatchTags(
                x.TeamA, x.TeamB,
                x.HomeRank, x.AwayRank,
                matchEntity?.HomeTeam?.AvgGoalsFor,
                matchEntity?.AwayTeam?.AvgGoalsFor,
                matchEntity?.HomeTeam?.IsStableTeam);

            // Deterministic explanation — does NOT affect scoring, only labels
            // the dominant factor behind this card's ranking.
            x.RecommendationReason = ResolveRecommendationReason(
                followsTeam,
                userBoost,
                x.UserTrendScore,
                x.MarketTrendScore,
                x.GlobalTrendScore,
                x.MomentumScore);

            x.Score = x.RecommendationScore * 100;
        }

        // ── R.13.4/R.13.5: Radar insights — overlay content + support ranking ─
        // RecommendationScore / UCB / Bandit are NOT modified. Radar only contributes a
        // low, capped (≤15%) support weight to the SORT KEY and fills content fields.
        var insights = await _feedInsightQuery.GetFeedAsync(int.MaxValue);
        var radarByMatch = insights
            .GroupBy(i => i.MatchId)
            .ToDictionary(g => g.Key, g => g.First());

        var finalList = result
            .Select(card =>
            {
                var radarScore = radarByMatch.TryGetValue(card.MatchId, out var ins)
                    ? _radarRanking.NormalizeRadarScore(ins)
                    : 0.0;
                card.RadarScore = radarScore;   // surface the already-computed Radar score
                var sortKey = _radarRanking.ComputeFinalScore(card.RecommendationScore, radarScore);
                return (card, sortKey);
            })
            .OrderByDescending(t => t.sortKey)
            .ThenBy(t => t.card.MatchId)   // deterministic tie-break → stable pagination
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => t.card)
            .ToList();

        // R.13.4 — overlay Radar content (StoryHeadline/StoryBody/AiSummary/InsightLabel)
        // onto cards that have an insight. Runs after sort/paginate; scores untouched.
        foreach (var card in finalList)
        {
            if (radarByMatch.TryGetValue(card.MatchId, out var insight))
                _radarFeedAdapter.Apply(card, insight);

            // Surface the already-computed Match Importance + Key Signals from the
            // intelligence snapshot. No new computation — read-and-map only.
            var snap = await _intelRepo.GetByMatchIdAsync(card.MatchId);
            if (snap != null)
            {
                card.MatchImportance = snap.ImportanceScore;
                card.KeySignals = MapKeySignals(snap.SignalsJson);
            }

            // Surface the existing Team.LogoUrl through the existing TeamDto contract.
            // The engine only set Name; the match entity already carries the logo.
            if (matchById.TryGetValue(card.MatchId, out var matchEntity))
            {
                // Mevcut Match.MatchDate'i feed'e taşı (yeni veri üretilmez).
                card.MatchDate = matchEntity.MatchDate;

                if (matchEntity.HomeTeam != null)
                {
                    card.HomeTeam.Id = matchEntity.HomeTeam.Id;
                    card.HomeTeam.LogoUrl = matchEntity.HomeTeam.LogoUrl;
                }
                if (matchEntity.AwayTeam != null)
                {
                    card.AwayTeam.Id = matchEntity.AwayTeam.Id;
                    card.AwayTeam.LogoUrl = matchEntity.AwayTeam.LogoUrl;
                }
            }
        }

        return finalList;
    }

    // Map the persisted MatchSignal[] JSON onto the frontend KeySignal contract.
    // Top 3 by weight; only real Label/Reason are carried (icon/value/tone stay null).
    private static List<KeySignalDto> MapKeySignals(string? signalsJson)
    {
        if (string.IsNullOrWhiteSpace(signalsJson)) return new();
        try
        {
            var signals = JsonSerializer.Deserialize<List<MatchSignal>>(signalsJson, _signalJsonOpts);
            if (signals == null) return new();

            return signals
                .OrderByDescending(s => s.Weight)
                .Take(3)
                .Select(s => new KeySignalDto { Title = s.Label, Caption = s.Reason })
                .ToList();
        }
        catch
        {
            return new();
        }
    }

    // ── Deterministic explanation layer ──────────────────────────────────────
    // Maps the dominant ranking factor to a stable reason code.
    // Pure read of already-computed signals — no scoring change, no AI text.
    private static string ResolveRecommendationReason(
        bool followsTeam,
        double userBoost,
        double userTrend,
        double marketTrend,
        double globalTrend,
        double momentum)
    {
        if (followsTeam) return "FOLLOWED_TEAM";
        if (userBoost >= 0.10) return "HIGH_INTEREST";

        // Compare weighted contributions (same weights the score uses).
        var normMomentum = momentum > 1 ? momentum / 100.0 : momentum;

        var cUser     = 0.5 * userTrend;
        var cMarket   = 0.3 * marketTrend;
        var cGlobal   = 0.3 * globalTrend;
        var cTrending = 0.2 * normMomentum;

        var max = Math.Max(Math.Max(cUser, cMarket), Math.Max(cGlobal, cTrending));

        if (max <= 0) return "GLOBAL_SIGNAL";
        if (max == cUser) return "HIGH_INTEREST";
        if (max == cMarket) return "MARKET_SIGNAL";
        if (max == cTrending) return "TRENDING";
        return "GLOBAL_SIGNAL";
    }

    // ── Story Layer ───────────────────────────────────────────────────────────

    private static readonly HashSet<(string, string)> DerbyPairs = new()
    {
        ("galatasaray", "fenerbahçe"), ("fenerbahçe", "galatasaray"),
        ("galatasaray", "beşiktaş"),   ("beşiktaş",   "galatasaray"),
        ("fenerbahçe", "beşiktaş"),    ("beşiktaş",   "fenerbahçe"),
        ("galatasaray", "trabzonspor"),("trabzonspor", "galatasaray"),
        ("fenerbahçe", "trabzonspor"), ("trabzonspor", "fenerbahçe"),
    };

    private static bool IsDerby(string home, string away) =>
        DerbyPairs.Contains((home.Trim().ToLowerInvariant(), away.Trim().ToLowerInvariant()));

    private static string BuildStoryHeadline(
        string homeName, string awayName,
        int? homeRank, int? awayRank,
        bool followsTeam, bool isHot, string? league)
    {
        if (followsTeam)                                              return "⭐ Takip Ettiğin Takım";
        if (IsDerby(homeName, awayName))                             return "⚔️ Dev Derbi";
        if (homeRank != null && awayRank != null
            && homeRank + awayRank <= 3)                             return "🏆 Liderlik Yarışı";
        if (homeRank != null && awayRank != null
            && homeRank + awayRank <= 5)                             return "🔝 Zirve Maçı";
        if (isHot)                                                   return "🔥 Bu Hafta Çok Konuşuluyor";
        var leagueLabel = string.IsNullOrWhiteSpace(league) ? "Lig" : league;
        return $"🏟️ {leagueLabel} Maçı";
    }

    private static string BuildStoryBody(
        string headline, string homeName, string awayName)
    {
        if (headline.StartsWith("⭐"))
            return $"{homeName} bu maçta {awayName} karşısında sahaya çıkıyor.";
        if (headline.StartsWith("⚔️"))
            return $"{homeName} ile {awayName} arasındaki derbi, taraftarların yakından takip ettiği karşılaşmalar arasında.";
        if (headline.StartsWith("🏆"))
            return $"{homeName} ile {awayName} arasındaki liderlik mücadelesi bu hafta sahaya taşınıyor.";
        if (headline.StartsWith("🔝"))
            return "İki takım da lig üst sıralarında. Bu maçın sonucu puan tablosunu sarsabilir.";
        if (headline.StartsWith("🔥"))
            return "Bu maç bu haftanın en çok konuşulan karşılaşmaları arasında.";
        return $"{homeName} evinde {awayName} ile karşılaşıyor.";
    }

    private static List<string> BuildMatchTags(
        string homeName, string awayName,
        int? homeRank, int? awayRank,
        double? homeAvgGoals, double? awayAvgGoals,
        bool? homeIsStable)
    {
        var tags = new List<string>();

        if (homeRank != null && awayRank != null && homeRank + awayRank <= 3)
            tags.Add("🏆 Liderlik Yarışı");
        else if (homeRank != null && awayRank != null && homeRank + awayRank <= 5)
            tags.Add("🔝 Zirve Maçı");

        if (IsDerby(homeName, awayName))
            tags.Add("⚔️ Derbi");

        if (homeAvgGoals != null && awayAvgGoals != null)
        {
            var total = homeAvgGoals.Value + awayAvgGoals.Value;
            if (total >= 3.0)
                tags.Add("⚽ Yüksek Gol Beklentisi");
            else if (total < 1.8)
                tags.Add("🛡️ Düşük Skorlu Geçebilir");
        }

        if (homeIsStable == true)
            tags.Add("📈 Ev Sahibi Formda");

        return tags.Take(3).ToList();
    }
}