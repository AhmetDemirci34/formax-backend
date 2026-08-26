using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Application.Services.Fixtures;
using Formax.Domain.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Formax.Application.Services.Players.Intelligence
{
    /// <summary>
    /// FORMAX Player Intelligence Engine — oyuncu-düzeyi zekâ üretir. Radar'ı okumaz/değiştirmez.
    /// Kaynaklar: MatchLineupPlayer (aday kadro), MatchPlayerStatus (uygunluk), evidence/haber
    /// (isim geçişi → News/Trend/Popularity), IPlayerStatsProvider (Form/Rating/Goals…).
    /// Eksik veri → sinyal Available=false (Graceful Fallback). IntelligenceScore + gerekçeleri
    /// bu engine üretir; HeroSelectionEngine yalnız okur. Sonuç IMemoryCache'te tutulur.
    /// </summary>
    public sealed class PlayerIntelligenceEngine : IPlayerIntelligenceEngine
    {
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);

        private readonly IMatchReadRepository _matches;
        private readonly ITeamReadRepository _teams;
        private readonly IMatchLineupRepository _lineups;
        private readonly IMatchPlayerStatusRepository _statuses;
        private readonly IMatchEvidenceRepository _evidence;
        private readonly FormaxMatchIdFactory _matchIdFactory;
        private readonly IPlayerStatsProvider _stats;
        private readonly IMemoryCache _cache;
        private readonly ILogger<PlayerIntelligenceEngine> _logger;

        public PlayerIntelligenceEngine(
            IMatchReadRepository matches, ITeamReadRepository teams, IMatchLineupRepository lineups,
            IMatchPlayerStatusRepository statuses, IMatchEvidenceRepository evidence,
            FormaxMatchIdFactory matchIdFactory, IPlayerStatsProvider stats,
            IMemoryCache cache, ILogger<PlayerIntelligenceEngine> logger)
        {
            _matches = matches; _teams = teams; _lineups = lineups; _statuses = statuses;
            _evidence = evidence; _matchIdFactory = matchIdFactory; _stats = stats;
            _cache = cache; _logger = logger;
        }

        public async Task<List<PlayerIntelligence>> GetSquadIntelligenceAsync(int matchId, CancellationToken ct = default)
        {
            var key = $"player-intel:{matchId}";
            if (_cache.TryGetValue(key, out List<PlayerIntelligence>? cached) && cached is not null)
                return cached;

            var result = await BuildAsync(matchId, ct);
            _cache.Set(key, result, CacheTtl);
            return result;
        }

        private async Task<List<PlayerIntelligence>> BuildAsync(int matchId, CancellationToken ct)
        {
            var match = _matches.GetById(matchId);
            if (match is null) return new List<PlayerIntelligence>();

            var squad = _lineups.GetPlayersByMatchId(matchId);
            if (squad.Count == 0) return new List<PlayerIntelligence>();

            var statuses = _statuses.GetByMatchId(matchId);
            var headlines = await LoadHeadlinesAsync(match, ct); // graceful — boş olabilir

            var list = new List<PlayerIntelligence>(squad.Count);
            foreach (var p in squad)
            {
                ct.ThrowIfCancellationRequested();
                var teamId = string.Equals(p.Side, "Home", StringComparison.OrdinalIgnoreCase)
                    ? match.HomeTeamId : match.AwayTeamId;

                var pi = new PlayerIntelligence
                {
                    PlayerName = p.PlayerName,
                    TeamId = teamId,
                    Side = p.Side,
                    LastUpdated = DateTime.UtcNow,
                };

                ApplyStatus(pi, statuses);
                ApplyNews(pi, headlines);
                await ApplyStatsAsync(pi, teamId, ct);

                Score(pi, p.IsCaptain);
                list.Add(pi);
            }

            return list;
        }

        // ── Kaynak: MatchPlayerStatus → Availability / Injury ────────────────────
        private static void ApplyStatus(PlayerIntelligence pi, List<MatchPlayerStatus> statuses)
        {
            var s = statuses.FirstOrDefault(x =>
                string.Equals(x.PlayerName, pi.PlayerName, StringComparison.OrdinalIgnoreCase));
            var text = (s?.Status ?? "").ToLowerInvariant();

            if (text.Contains("injur") || text.Contains("sakat") || text.Contains("out"))
            { pi.Availability = PlayerSignal.Of(10, 70); pi.Injury = PlayerSignal.Of(90, 70); }
            else if (text.Contains("doubt") || text.Contains("şüphe") || text.Contains("questionable"))
            { pi.Availability = PlayerSignal.Of(50, 60); pi.Injury = PlayerSignal.Of(50, 60); }
            else if (text.Contains("suspend") || text.Contains("cezal") || text.Contains("ban"))
            { pi.Availability = PlayerSignal.Of(0, 80); pi.Injury = PlayerSignal.Missing(); }
            else
            { pi.Availability = PlayerSignal.Of(s is null ? 90 : 100, s is null ? 40 : 55); pi.Injury = PlayerSignal.Missing(); }
        }

        // ── Kaynak: evidence/haber başlıklarında isim geçişi → News/Trend/Popularity ──
        private static void ApplyNews(PlayerIntelligence pi, IReadOnlyList<string> headlines)
        {
            if (headlines.Count == 0) return; // graceful — sinyaller Missing kalır

            var token = LastNameToken(pi.PlayerName);
            if (token.Length < 4) return;

            var mentions = headlines.Count(h => h.Contains(token, StringComparison.OrdinalIgnoreCase));
            if (mentions == 0) return;

            var val = Math.Min(100, mentions * 22);
            pi.NewsImpact = PlayerSignal.Of(val, 60);
            pi.Trend = PlayerSignal.Of(Math.Min(100, val + 8), 55);
            pi.Popularity = PlayerSignal.Of(Math.Min(100, mentions * 18), 55);
        }

        // ── Kaynak: IPlayerStatsProvider → Form/Rating/Goals/Assists/Minutes/… ───
        private async Task ApplyStatsAsync(PlayerIntelligence pi, int teamId, CancellationToken ct)
        {
            if (!_stats.IsEnabled) return; // graceful — istatistik sinyalleri Missing kalır

            PlayerStats? st;
            try { st = await _stats.GetPlayerStatsAsync(pi.PlayerName, teamId, ct); }
            catch (Exception ex) { _logger.LogWarning(ex, "[PLAYER-INTEL] stats alınamadı: {Name}", pi.PlayerName); return; }
            if (st is null || !st.Found) return;

            pi.ExternalPlayerId = st.ExternalPlayerId;
            var form = st.FormRating ?? st.Rating;
            if (form is double f) { pi.Form = PlayerSignal.Of(Clamp(f * 10), 75); pi.RecentPerformance = PlayerSignal.Of(Clamp(f * 10), 65); }
            if (st.Rating is double r) pi.Rating = PlayerSignal.Of(Clamp(r * 10), 75);
            if (st.Goals is int g) pi.Goals = PlayerSignal.Of(Math.Min(100, g * 12), 70);
            if (st.Assists is int a) pi.Assists = PlayerSignal.Of(Math.Min(100, a * 14), 65);
            if (st.Minutes is int m) pi.Minutes = PlayerSignal.Of(Math.Min(100, m / 30), 60);
            pi.ExpectedImpact = PlayerSignal.Of(
                Clamp((st.Rating ?? 6) * 6 + (st.Goals ?? 0) * 3 + (st.Assists ?? 0) * 2), 60);
        }

        // ── IntelligenceScore + PrimaryReason/SecondaryReason ────────────────────
        private static void Score(PlayerIntelligence pi, bool isCaptain)
        {
            var comps = new List<(string Reason, double Weighted, double Value)>();
            void Add(PlayerSignal s, double weight, string reason)
            {
                if (!s.Available) return;
                var w = weight * (s.Confidence / 100.0);
                comps.Add((reason, s.Value * w, s.Value));
            }

            Add(pi.Form, 1.4, "In Form");
            Add(pi.Rating, 1.3, "Top Rated");
            Add(pi.ExpectedImpact, 1.3, "Match Changer");
            Add(pi.RecentPerformance, 1.2, "Rising");
            Add(pi.Goals, 1.1, "Top Scorer");
            Add(pi.Assists, 1.0, "Playmaker");
            Add(pi.NewsImpact, 1.0, "Most Talked");
            Add(pi.Trend, 0.9, "Trending");
            Add(pi.Popularity, 0.8, "Fan Favourite");
            Add(pi.Confidence, 0.7, "High Confidence");
            Add(pi.Minutes, 0.6, "Ever-present");

            double baseScore;
            if (comps.Count == 0)
            {
                baseScore = isCaptain ? 45 : 35; // hiç veri yoksa nötr taban
            }
            else
            {
                var totalW = 1.4 * Avail(pi.Form) + 1.3 * Avail(pi.Rating) + 1.3 * Avail(pi.ExpectedImpact)
                    + 1.2 * Avail(pi.RecentPerformance) + 1.1 * Avail(pi.Goals) + 1.0 * Avail(pi.Assists)
                    + 1.0 * Avail(pi.NewsImpact) + 0.9 * Avail(pi.Trend) + 0.8 * Avail(pi.Popularity)
                    + 0.7 * Avail(pi.Confidence) + 0.6 * Avail(pi.Minutes);
                baseScore = totalW > 0 ? comps.Sum(c => c.Weighted) / totalW : 40;
            }

            if (isCaptain) baseScore += 4;

            // Availability/Injury kapıları
            if (pi.Availability.Available) baseScore *= 0.5 + (pi.Availability.Value / 200.0); // 0.5–1.0
            if (pi.Injury.Available) baseScore *= 1.0 - (pi.Injury.Value / 250.0);              // sakatlık cezası

            pi.IntelligenceScore = (int)Math.Round(Math.Clamp(baseScore, 0, 100));

            var ordered = comps.OrderByDescending(c => c.Weighted).ToList();
            pi.PrimaryReason = ordered.Count > 0 ? ordered[0].Reason : (isCaptain ? "Team Captain" : "Squad Member");
            pi.SecondaryReason = ordered.Count > 1 ? ordered[1].Reason : "";
        }

        // ── Yardımcılar ──────────────────────────────────────────────────────────
        private async Task<IReadOnlyList<string>> LoadHeadlinesAsync(Match match, CancellationToken ct)
        {
            try
            {
                var home = _teams.GetById(match.HomeTeamId)?.Name ?? "";
                var away = _teams.GetById(match.AwayTeamId)?.Name ?? "";
                var formaxId = _matchIdFactory.Create(match.MatchDate, home, away);
                var ctx = await _evidence.GetContextAsync(formaxId, home, away, ct);
                var all = new List<string>();
                all.AddRange(ctx.TopHeadlines);
                all.AddRange(ctx.LatestHeadlines);
                all.AddRange(ctx.TopEvidence.Select(e => e.Headline));
                return all.Where(h => !string.IsNullOrWhiteSpace(h)).Distinct().ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[PLAYER-INTEL] evidence başlıkları alınamadı (graceful)");
                return Array.Empty<string>();
            }
        }

        private static double Avail(PlayerSignal s) => s.Available ? 1.0 : 0.0;
        private static double Clamp(double v) => Math.Clamp(v, 0, 100);
        private static string LastNameToken(string name)
        {
            var parts = (name ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? "" : parts[^1];
        }
    }
}
