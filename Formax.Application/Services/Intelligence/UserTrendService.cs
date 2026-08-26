using System.Globalization;
using System.Text;
using Formax.Application.Interfaces.Repositories;
using Formax.Application.Services.Recommendation;
using Formax.Domain.Entities;

namespace Formax.Application.Services.Intelligence;

public class UserTrendService
{
    // İkinci davranışsal kaynağın (UserInterestScores) ağırlığı — UserActions'a göre İKİNCİL.
    // interestAffinity ∈ [0,1] olduğundan bu terim mevcut tanh zarfı içinde doğal olarak
    // sınırlanır; ilgi verisi yoksa 0 → UserActions davranışı BİREBİR korunur.
    private const double INTEREST_WEIGHT = 0.20;

    private readonly IUserActionRepository _repo;
    private readonly IUserInterestScoreRepository _interestRepo;
    private readonly InterestDecayService _decay;

    public UserTrendService(
        IUserActionRepository repo,
        IUserInterestScoreRepository interestRepo,
        InterestDecayService decay)
    {
        _repo = repo;
        _interestRepo = interestRepo;
        _decay = decay;
    }

    public async Task<double> Calculate(
        int userId, int matchId, string teamA, string teamB, double currentOdds, string? league = null)
    {
        var actions = await _repo.GetByUserIdAsync(userId);

        // İKİNCİ DAVRANIŞSAL KAYNAK: UserInterestScores (Team + League). Ham int Score
        // KULLANILMAZ; kullanıcının kendi max'ına göre 0..1 normalize + read-time decay.
        var interestAffinity = await ComputeInterestAffinity(userId, teamA, teamB, league);

        if (actions.Count == 0)
            // Mevcut cold-start davranışı korunur (0.3); yalnız ilgi verisi VARSA doğal dahil edilir.
            return interestAffinity > 0
                ? Math.Tanh(0.3 + interestAffinity * INTEREST_WEIGHT)
                : 0.3;

        // 🔹 BASE USER (çok düşük etki)
        double totalScore = 0;

        foreach (var a in actions)
        {
            double score = 0;

            if (a.ViewDurationMs > 0)
            {
                if (a.ViewDurationMs < 2000) score = 0.2;
                else if (a.ViewDurationMs < 5000) score = 0.5;
                else if (a.ViewDurationMs < 10000) score = 0.7;
                else score = 1.0;

                if (a.OpenedDetail) score += 0.2;
                if (a.Followed) score += 0.3;
            }
            else
            {
                if (a.ActionType == 1) score = 0.8;
                else if (a.ActionType == -1) score = 0.1;
                else score = 0.5;
            }

            totalScore += Math.Clamp(score, 0, 1);
        }

        var userBaseScore = totalScore / actions.Count;

        // 🔹 MATCH BOOST
        double matchBoost = 0;

        var matchActions = actions
            .Where(x => x.MatchId == matchId)
            .ToList();

        if (matchActions.Count > 0)
        {
            matchBoost = matchActions
                .Select(x =>
                {
                    if (x.ViewDurationMs > 8000) return 0.3;
                    if (x.ActionType == 1) return 0.25;
                    return 0.05;
                })
                .Max();
        }

        // 🔥 TEAM BOOST (MAX + INTENSITY)
        double teamBoost = 0;

        var teamActions = actions
            .Where(x => x.Team == teamA || x.Team == teamB)
            .ToList();

        if (teamActions.Count > 0)
        {
            var baseBoost = teamActions
                .Select(x =>
                {
                    double score = 0.1;

                    if (x.ViewDurationMs > 8000) score += 0.4;
                    if (x.ActionType == 1) score += 0.3;
                    if (x.Followed) score += 0.2;

                    return score;
                })
                .Max();

            var intensity = teamActions.Count;

            // 🔥 kritik fark
            teamBoost = baseBoost * (1 + Math.Min(intensity * 0.15, 1.5));
        }

        // 🔥 ODDS BEHAVIOR
        double oddsBoost = 0;

        var similarOdds = actions
            .Where(x => Math.Abs(x.Odds - currentOdds) < 0.3)
            .ToList();

        if (similarOdds.Count > 0)
        {
            oddsBoost = similarOdds
                .Select(x =>
                {
                    if (x.ActionType == 1) return 0.3;
                    if (x.ActionType == -1) return 0.1;
                    return 0.2;
                })
                .Max();
        }

        // 🔥 RECENCY
        double recencyBoost = 0;

        var recent = actions
            .Where(x => x.CreatedAt > DateTime.UtcNow.AddHours(-6))
            .ToList();

        if (recent.Count > 0)
        {
            recencyBoost = recent
                .Select(x =>
                {
                    if (x.ActionType == 1) return 0.3;
                    return 0.1;
                })
                .Max();
        }

        // Exploration is handled deterministically by the MatchBandit UCB term
        // in GetRecommendationFeedUseCase. A second per-request random exploration
        // here made the recommendation score non-deterministic between requests,
        // shifting the global ordering and causing the same match to appear on
        // two pages (duplicate React key). Removed — single exploration source.

        // 🔥 FINAL SCORE (AGRESİF + AKILLI)
        var final =
            (teamBoost * 0.65) +   // 🔥 ana sinyal (UserActions — BİRİNCİL davranışsal kaynak)
            (oddsBoost * 0.10) +
            (recencyBoost * 0.10) +
            (matchBoost * 0.10) +
            (userBaseScore * 0.03) +
            (interestAffinity * INTEREST_WEIGHT);  // İKİNCİL davranışsal kaynak (UserInterestScores)

        return Math.Tanh(final);
    }

    // ── İKİNCİ DAVRANIŞSAL KAYNAK: UserInterestScores → normalize affinity (0..1) ──────────
    // Ham int Score DOĞRUDAN kullanılmaz: kullanıcının kendi max skoruna göre göreli normalize
    // (decay yokluğuna dayanıklı) + read-time InterestDecayService (stored skoru bozmadan).
    // Team: HomeTeam + AwayTeam İKİSİ değerlendirilir (tek-takım varsayımı yok). League: aynı
    // davranışsal modele dahildir. ContentType KULLANILMAZ (kartlar arası ayrıştırıcı değil).
    private async Task<double> ComputeInterestAffinity(int userId, string teamA, string teamB, string? league)
    {
        var scores = await _interestRepo.GetByUser(userId);
        if (scores.Count == 0) return 0.0;

        var now = DateTime.UtcNow;
        int Decayed(UserInterestScore s) => _decay.ApplyDecay(s.Score, s.LastEventAtUtc, now);

        var teamRows = scores.Where(s => s.Layer == "team").ToList();
        var leagueRows = scores.Where(s => s.Layer == "league").ToList();

        double maxTeam = teamRows.Count > 0 ? teamRows.Max(s => (double)Decayed(s)) : 0.0;
        double maxLeague = leagueRows.Count > 0 ? leagueRows.Max(s => (double)Decayed(s)) : 0.0;

        double TeamScore(string name)
        {
            var key = Normalize(name);
            var row = teamRows.FirstOrDefault(s => s.Key == key);
            return row == null ? 0.0 : Decayed(row);
        }

        double LeagueScore(string name)
        {
            var key = Normalize(name);
            var row = leagueRows.FirstOrDefault(s => s.Key == key);
            return row == null ? 0.0 : Decayed(row);
        }

        // HomeTeam + AwayTeam ikisi de: güçlü olan taraf.
        double teamAff = maxTeam > 0
            ? Math.Max(TeamScore(teamA), TeamScore(teamB)) / maxTeam
            : 0.0;

        double leagueAff = (!string.IsNullOrWhiteSpace(league) && maxLeague > 0)
            ? LeagueScore(league) / maxLeague
            : 0.0;

        // Team + League birlikte; güçlü olan (sinyaller arası uydurma weight yok).
        return Math.Max(teamAff, leagueAff);
    }

    // UserInterestScoreRepository'nin key normalizasyonuyla BİREBİR aynı (lookup eşleşmesi için).
    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            if (char.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}