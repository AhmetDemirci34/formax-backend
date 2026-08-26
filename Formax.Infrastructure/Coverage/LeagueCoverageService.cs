using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Application.Coverage;
using Formax.Application.Interfaces;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace Formax.Infrastructure.Coverage
{
    /// <summary>
    /// GDP League Coverage Intelligence — GDP'nin coverage'ını GERÇEK ingestion verisinden (canonical DB)
    /// hesaplar. Yeni provider/API çağrısı YOK; yalnız mevcut tablolar okunur. Per-league temiz join olan
    /// capability'ler (Competition/Standings/Prediction/Statistics/Availability/Live/Referee/Venue/TeamProfile)
    /// per-league; News/Social/Weather (FormaxMatchId iki-dünya) GLOBAL-scope dürüstçe hesaplanır. Sonuç
    /// 5 dk cache'lenir. Eşikler/ağırlıklar CONFIG'ten gelir (hardcode değil; kod yalnız makul varsayılan).
    /// </summary>
    public sealed class LeagueCoverageService : ILeagueCoverageService
    {
        private readonly FormaxDbContext _db;
        private readonly IConfiguration _config;
        private readonly IMemoryCache _cache;
        private const string CacheKey = "gdp:league-coverage:v1";
        private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

        // Trend (in-memory, process ömrü boyunca) — Coverage History/Trend diagnostiği için.
        private static readonly object _trendLock = new();
        private static readonly List<CoverageSnapshot> _trend = new();

        // Motorun GERÇEKTEN okuduğu capability'ler (MarketProbabilityEngine readiness için).
        private static readonly string[] MotorCaps =
            { "Competition", "Standings", "Prediction", "Statistics", "Availability", "Live" };

        public LeagueCoverageService(FormaxDbContext db, IConfiguration config, IMemoryCache cache)
        {
            _db = db;
            _config = config;
            _cache = cache;
        }

        public IReadOnlyList<LeagueCoverage> ComputeAll()
        {
            if (_cache.TryGetValue(CacheKey, out List<LeagueCoverage>? cached) && cached != null)
                return cached;

            var result = ComputeInternal();
            _cache.Set(CacheKey, result, Ttl);
            RecordTrend(result);
            return result;
        }

        public LeagueCoverage? Compute(int leagueId)
            => ComputeAll().FirstOrDefault(l => l.LeagueId == leagueId);

        public string TierFor(int leagueId)
            => ComputeAll().FirstOrDefault(l => l.LeagueId == leagueId)?.Tier ?? "Passive";

        public IReadOnlyDictionary<int, int> ScoreMap()
            => ComputeAll().ToDictionary(l => l.LeagueId, l => l.OverallScore);

        public IReadOnlyList<CoverageSnapshot> Trend()
        {
            lock (_trendLock) return _trend.ToList();
        }

        public GdpReadiness ComputeReadiness()
        {
            var all = ComputeAll();
            if (all.Count == 0) return new GdpReadiness();

            // Capability platform readiness = maç-ağırlıklı ortalama oran.
            var capNames = all.SelectMany(l => l.Capabilities.Select(c => c.Name)).Distinct().ToList();
            var capReadiness = new Dictionary<string, int>();
            foreach (var cap in capNames)
            {
                double num = 0, den = 0;
                foreach (var l in all)
                {
                    var c = l.Capabilities.FirstOrDefault(x => x.Name == cap);
                    if (c == null) continue;
                    var w = Math.Max(1, l.MatchCount);
                    num += c.CoverageRatio * w;
                    den += w;
                }
                capReadiness[cap] = den > 0 ? (int)Math.Round(num / den * 100) : 0;
            }

            var totalMatches = all.Sum(l => (double)Math.Max(1, l.MatchCount));
            int MatchWeighted(Func<LeagueCoverage, int> sel) =>
                (int)Math.Round(all.Sum(l => sel(l) * Math.Max(1, l.MatchCount)) / totalMatches);

            var overall = MatchWeighted(l => l.OverallScore);
            var motor = (int)Math.Round(MotorCaps.Where(capReadiness.ContainsKey).Select(c => capReadiness[c]).DefaultIfEmpty(0).Average());
            var aiCaps = MotorCaps.Concat(new[] { "News", "Social" }).Where(capReadiness.ContainsKey).ToList();
            var ai = (int)Math.Round(aiCaps.Select(c => capReadiness[c]).DefaultIfEmpty(0).Average());

            // ── OPERATIONAL: yalnız aktif penceredeki (yaklaşan maçı olan) ligler, yaklaşan-maç-ağırlıklı. ──
            var opLeagues = all.Where(l => l.OperationalActive).ToList();
            var opCapReadiness = new Dictionary<string, int>();
            int opOverall = 0, opMotor = 0, opAi = 0;
            if (opLeagues.Count > 0)
            {
                var opCapNames = opLeagues.SelectMany(l => l.OperationalCapabilities.Select(c => c.Name)).Distinct().ToList();
                foreach (var cap in opCapNames)
                {
                    double num = 0, den = 0;
                    foreach (var l in opLeagues)
                    {
                        var c = l.OperationalCapabilities.FirstOrDefault(x => x.Name == cap);
                        if (c == null) continue;
                        var w = Math.Max(1, l.UpcomingCount);
                        num += c.CoverageRatio * w; den += w;
                    }
                    opCapReadiness[cap] = den > 0 ? (int)Math.Round(num / den * 100) : 0;
                }
                var opTotal = opLeagues.Sum(l => (double)Math.Max(1, l.UpcomingCount));
                opOverall = (int)Math.Round(opLeagues.Sum(l => l.OperationalScore * Math.Max(1, l.UpcomingCount)) / opTotal);
                opMotor = (int)Math.Round(MotorCaps.Where(opCapReadiness.ContainsKey).Select(c => opCapReadiness[c]).DefaultIfEmpty(0).Average());
                var opAiCaps = MotorCaps.Concat(new[] { "News", "Social" }).Where(opCapReadiness.ContainsKey).ToList();
                opAi = (int)Math.Round(opAiCaps.Select(c => opCapReadiness[c]).DefaultIfEmpty(0).Average());
            }

            return new GdpReadiness
            {
                OverallGdpReadiness = overall,
                MarketProbabilityEngineReadiness = motor,
                AiReadiness = ai,
                LeagueCount = all.Count,
                PremiumLeagues = all.Count(l => l.Tier == "Premium"),
                StandardLeagues = all.Count(l => l.Tier == "Standard"),
                LimitedLeagues = all.Count(l => l.Tier == "Limited"),
                PassiveLeagues = all.Count(l => l.Tier == "Passive"),
                CapabilityReadiness = capReadiness,
                OperationalCoverage = opOverall,
                OperationalMotorReadiness = opMotor,
                OperationalAiReadiness = opAi,
                OperationalLeagueCount = opLeagues.Count,
                OperationalCapabilityReadiness = opCapReadiness
            };
        }

        // ══════════════════════════════ Çekirdek hesap ══════════════════════════════

        private List<LeagueCoverage> ComputeInternal()
        {
            var now = DateTime.UtcNow;
            var from = now.AddDays(-1);
            var to = now.AddDays(7);

            // ── Maç düzeyi per-league agregatlar (tek grup sorgu) ──
            // GRUPLAMA YALNIZ LeagueId İLE. Ölçüldü (14.08): eskiden (LeagueId + League ADI)
            // ile gruplanıyordu; aynı lig sağlayıcıda iki farklı adla geçtiğinde (268 =
            // "Primera División" ve "Primera División - Apertura", 17, 1025, 1026) AYNI
            // LeagueId için İKİ satır üretiyordu ve ScoreMap()'in ToDictionary'si
            // "An item with the same key has already been added. Key: 268" ile PATLIYORDU.
            // Bu istisna WorldPerceptionDailyJob'ın standings ve competition-context
            // yenilemesini fetch'e girmeden öldürüyordu → LeagueStandings ve
            // CompetitionContexts aylardır boş kalmıştı (maç önemi/tur/puan durumu yok).
            // Kimlik zaten LeagueId'dir; ad yalnız etikettir, en sık kullanılanı seçilir.
            var matchAgg = _db.Matches.Where(m => m.LeagueId > 0)
                .GroupBy(m => new { m.LeagueId, m.League })
                .Select(g => new
                {
                    g.Key.LeagueId,
                    g.Key.League,
                    Total = g.Count(),
                    WithReferee = g.Count(m => m.Referee != null && m.Referee != ""),
                    WithVenue = g.Count(m => m.Venue != null && m.Venue != ""),
                    Upcoming = g.Count(m => m.MatchDate >= from && m.MatchDate <= to)
                })
                .ToList()
                .GroupBy(x => x.LeagueId)
                .Select(g => new
                {
                    LeagueId = g.Key,
                    League = g.OrderByDescending(x => x.Total).First().League,
                    Total = g.Sum(x => x.Total),
                    WithReferee = g.Sum(x => x.WithReferee),
                    WithVenue = g.Sum(x => x.WithVenue),
                    Upcoming = g.Sum(x => x.Upcoming)
                })
                .ToList();
            if (matchAgg.Count == 0) return new List<LeagueCoverage>();

            // ── Takım↔lig eşlemesi (distinct) ──
            var teamPairs = _db.Matches.Where(m => m.LeagueId > 0).Select(m => new { m.LeagueId, T = m.HomeTeamId })
                .Union(_db.Matches.Where(m => m.LeagueId > 0).Select(m => new { m.LeagueId, T = m.AwayTeamId }))
                .ToList();
            var teamsByLeague = teamPairs.GroupBy(p => p.LeagueId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.T).Distinct().ToHashSet());

            // ── Capability sayıları (join → league) ──
            Dictionary<int, int> ByLeagueFromMatch(IQueryable<int> matchIds) =>
                matchIds.Join(_db.Matches, id => id, m => m.Id, (id, m) => m.LeagueId)
                        .GroupBy(l => l).Select(g => new { L = g.Key, C = g.Count() })
                        .ToList().ToDictionary(x => x.L, x => x.C);

            var compByLeague = ByLeagueFromMatch(_db.CompetitionContexts.Select(c => c.MatchId));
            var predByLeague = ByLeagueFromMatch(_db.MatchPredictionSignals.Select(p => p.MatchId));
            var availByLeague = ByLeagueFromMatch(_db.MatchPlayerStatuses.Select(s => s.MatchId).Distinct());
            var liveByLeague = ByLeagueFromMatch(_db.MatchLiveStats
                .Where(s => s.ShotsHome > 0 || s.ShotsAway > 0 || s.PossessionHome > 0 || s.DangerousAttacksHome > 0)
                .Select(s => s.MatchId));

            // ── OPERATIONAL: aynı per-maç capability'ler AMA payda = AKTİF pencere (yaklaşan maçlar). ──
            // GLOBAL payda (ligin tüm-zaman maçları) operasyonel capability'yi yapısal olarak seyreltir;
            // operasyonel sayı yalnız hedeflenen aktif slate'i ölçer.
            Dictionary<int, int> ByLeagueInWindow(IQueryable<int> matchIds) =>
                matchIds.Join(_db.Matches.Where(mm => mm.MatchDate >= from && mm.MatchDate <= to),
                              id => id, m => m.Id, (id, m) => m.LeagueId)
                        .GroupBy(l => l).Select(g => new { L = g.Key, C = g.Count() })
                        .ToList().ToDictionary(x => x.L, x => x.C);

            var compWin = ByLeagueInWindow(_db.CompetitionContexts.Select(c => c.MatchId));
            var predWin = ByLeagueInWindow(_db.MatchPredictionSignals.Select(p => p.MatchId));
            var availWin = ByLeagueInWindow(_db.MatchPlayerStatuses.Select(s => s.MatchId).Distinct());
            var liveWin = ByLeagueInWindow(_db.MatchLiveStats
                .Where(s => s.ShotsHome > 0 || s.ShotsAway > 0 || s.PossessionHome > 0 || s.DangerousAttacksHome > 0)
                .Select(s => s.MatchId));

            var standingsByLeague = _db.LeagueStandings.GroupBy(s => s.LeagueId)
                .Select(g => new { L = g.Key, C = g.Count() }).ToList().ToDictionary(x => x.L, x => x.C);
            var statsByLeague = _db.TeamSeasonStatistics.GroupBy(s => s.LeagueId)
                .Select(g => new { L = g.Key, C = g.Select(x => x.TeamId).Distinct().Count() }).ToList()
                .ToDictionary(x => x.L, x => x.C);
            var profileTeamIds = _db.TeamProfileSignals.Select(p => p.TeamId).ToHashSet();

            // ── Kapasite tazelikleri (global max UpdatedAt → freshness) ──
            double FreshOf(DateTime? maxUpdated) => maxUpdated == null ? 0 : FreshnessFromAge((now - maxUpdated.Value).TotalDays);
            var freshStandings = FreshOf(_db.LeagueStandings.Any() ? _db.LeagueStandings.Max(s => (DateTime?)s.UpdatedAt) : null);
            var freshStats = FreshOf(_db.TeamSeasonStatistics.Any() ? _db.TeamSeasonStatistics.Max(s => (DateTime?)s.UpdatedAt) : null);
            var freshPred = FreshOf(_db.MatchPredictionSignals.Any() ? _db.MatchPredictionSignals.Max(s => (DateTime?)s.UpdatedAt) : null);
            var freshComp = FreshOf(_db.CompetitionContexts.Any() ? _db.CompetitionContexts.Max(s => (DateTime?)s.UpdatedAt) : null);
            var freshLive = FreshOf(_db.MatchLiveStats.Any() ? _db.MatchLiveStats.Max(s => (DateTime?)s.UpdatedAt) : null);
            var freshProfile = FreshOf(_db.TeamProfileSignals.Any() ? _db.TeamProfileSignals.Max(s => (DateTime?)s.UpdatedAt) : null);

            // ── GLOBAL (iki-dünya) capability'ler: News/Social/Weather ──
            var fixtureCount = Math.Max(1, _db.Fixtures.Count());
            var newsFmx = _db.MatchEvidenceRecords.Select(e => e.FormaxMatchId).Distinct().Count();
            var socialFmx = _db.SocialPosts.Select(s => s.FormaxMatchId).Distinct().Count();
            var weatherCount = _db.MatchWeathers.Count();
            var newsRatio = Math.Clamp((double)newsFmx / fixtureCount, 0, 1);
            var socialRatio = Math.Clamp((double)socialFmx / fixtureCount, 0, 1);
            var weatherRatio = weatherCount > 0 ? Math.Clamp((double)weatherCount / fixtureCount, 0, 1) : 0.0;

            var weights = CapabilityWeights();
            var (premium, standard, limited) = Tiers();
            var (supportedR, partialR) = StatusThresholds();

            var list = new List<LeagueCoverage>(matchAgg.Count);
            foreach (var m in matchAgg)
            {
                var lid = m.LeagueId;
                var teamCount = teamsByLeague.TryGetValue(lid, out var ts) ? ts.Count : 0;
                var profilesInLeague = teamCount > 0 ? ts!.Count(id => profileTeamIds.Contains(id)) : 0;

                double Ratio(Dictionary<int, int> d, int denom) =>
                    denom <= 0 ? 0 : Math.Clamp((double)(d.TryGetValue(lid, out var c) ? c : 0) / denom, 0, 1);

                // Lig-düzeyi (per-maç olmayan) oranlar — GLOBAL ve OPERATIONAL'de ortak (ligi tanımlar).
                var standingsRatio = standingsByLeague.ContainsKey(lid) ? Math.Min(1.0, standingsByLeague[lid] / 18.0) : 0;
                var standingsDetail = standingsByLeague.TryGetValue(lid, out var sc) ? $"{sc} satır" : "yok";
                var statsRatio = teamCount > 0 && statsByLeague.TryGetValue(lid, out var st) ? Math.Clamp((double)st / teamCount, 0, 1) : 0;
                var profileRatio = teamCount > 0 ? Math.Clamp((double)profilesInLeague / teamCount, 0, 1) : 0;
                var refereeRatio = m.Total > 0 ? Math.Clamp((double)m.WithReferee / m.Total, 0, 1) : 0;
                var venueRatio = m.Total > 0 ? Math.Clamp((double)m.WithVenue / m.Total, 0, 1) : 0;

                var caps = new List<CapabilityCoverage>
                {
                    // GLOBAL: per-maç capability payda = ligin TÜM maçları (tüm-zaman).
                    Cap("Competition", Ratio(compByLeague, m.Total), freshComp, 90, supportedR, partialR),
                    Cap("Standings", standingsRatio, freshStandings, 92, supportedR, partialR, detail: standingsDetail),
                    Cap("Prediction", Ratio(predByLeague, m.Total), freshPred, 88, supportedR, partialR),
                    Cap("Statistics", statsRatio, freshStats, 90, supportedR, partialR),
                    Cap("Availability", Ratio(availByLeague, m.Total), freshLive, 90, supportedR, partialR),
                    Cap("Live", Ratio(liveByLeague, m.Total), freshLive, 90, supportedR, partialR,
                        detail: "zengin istatistik (şut/topa sahip olma)"),
                    Cap("TeamProfile", profileRatio, freshProfile, 88, supportedR, partialR),
                    Cap("Referee", refereeRatio, 1.0, 80, supportedR, partialR),
                    Cap("Venue", venueRatio, 1.0, 80, supportedR, partialR),
                    // GLOBAL (iki-dünya) — per-league temiz join yok, dürüstçe platform-geneli.
                    Cap("News", newsRatio, 0.8, 85, supportedR, partialR, scope: "Global"),
                    Cap("Social", socialRatio, 0.8, 90, supportedR, partialR, scope: "Global"),
                    Cap("Weather", weatherRatio, 0.0, 0, supportedR, partialR, scope: "Global",
                        detail: weatherCount == 0 ? "hiç veri yok" : "")
                };

                // OPERATIONAL: per-maç capability payda = AKTİF pencere (yaklaşan maç); lig-düzeyi oranlar aynı.
                var upc = m.Upcoming;
                double OpRatio(Dictionary<int, int> d) =>
                    upc <= 0 ? 0 : Math.Clamp((double)(d.TryGetValue(lid, out var c) ? c : 0) / upc, 0, 1);
                var opCaps = new List<CapabilityCoverage>
                {
                    Cap("Competition", OpRatio(compWin), freshComp, 90, supportedR, partialR),
                    Cap("Standings", standingsRatio, freshStandings, 92, supportedR, partialR, detail: standingsDetail),
                    Cap("Prediction", OpRatio(predWin), freshPred, 88, supportedR, partialR),
                    Cap("Statistics", statsRatio, freshStats, 90, supportedR, partialR),
                    Cap("Availability", OpRatio(availWin), freshLive, 90, supportedR, partialR),
                    Cap("Live", OpRatio(liveWin), freshLive, 90, supportedR, partialR,
                        detail: "zengin istatistik (şut/topa sahip olma)"),
                    Cap("TeamProfile", profileRatio, freshProfile, 88, supportedR, partialR),
                    Cap("Referee", refereeRatio, 1.0, 80, supportedR, partialR),
                    Cap("Venue", venueRatio, 1.0, 80, supportedR, partialR),
                    Cap("News", newsRatio, 0.8, 85, supportedR, partialR, scope: "Global"),
                    Cap("Social", socialRatio, 0.8, 90, supportedR, partialR, scope: "Global"),
                    Cap("Weather", weatherRatio, 0.0, 0, supportedR, partialR, scope: "Global",
                        detail: weatherCount == 0 ? "hiç veri yok" : "")
                };

                // Ağırlıklı oran ortalaması (yalnız tanımlı ağırlıklar) — global ve operasyonel için ortak formül.
                int WeightedScore(List<CapabilityCoverage> cs)
                {
                    double wsum = 0, vsum = 0;
                    foreach (var c in cs)
                    {
                        var w = weights.TryGetValue(c.Name, out var ww) ? ww : 0.02;
                        wsum += w; vsum += w * c.CoverageRatio;
                    }
                    return wsum > 0 ? (int)Math.Round(vsum / wsum * 100) : 0;
                }
                var score = WeightedScore(caps);
                var tier = score >= premium ? "Premium" : score >= standard ? "Standard" : score >= limited ? "Limited" : "Passive";

                list.Add(new LeagueCoverage
                {
                    LeagueId = lid,
                    LeagueName = m.League,
                    MatchCount = m.Total,
                    TeamCount = teamCount,
                    UpcomingCount = m.Upcoming,
                    Capabilities = caps,
                    OverallScore = score,
                    Tier = tier,
                    OperationalCapabilities = opCaps,
                    OperationalScore = upc > 0 ? WeightedScore(opCaps) : 0,
                    OperationalActive = upc > 0
                });
            }

            return list.OrderByDescending(l => l.OverallScore).ThenByDescending(l => l.UpcomingCount).ToList();
        }

        private static CapabilityCoverage Cap(string name, double ratio, double freshness, int trust,
            double supportedR, double partialR, string scope = "PerLeague", string detail = "")
        {
            ratio = Math.Clamp(ratio, 0, 1);
            var status = ratio >= supportedR ? "Supported" : ratio > partialR ? "Partial" : "Unavailable";
            var dq = Math.Round(ratio * 0.5 + freshness * 0.3 + (trust / 100.0) * 0.2, 3);
            return new CapabilityCoverage
            {
                Name = name, Status = status, CoverageRatio = Math.Round(ratio, 3),
                Freshness = Math.Round(freshness, 3), SourceTrust = ratio > 0 ? trust : 0,
                EvidenceScore = (int)Math.Round(ratio * 100), DataQuality = ratio > 0 ? dq : 0,
                Scope = scope, Detail = detail
            };
        }

        /// <summary>Yaş (gün) → tazelik 0..1: ≤1g=1.0, 14g'de 0'a düşer.</summary>
        private static double FreshnessFromAge(double ageDays)
        {
            if (ageDays <= 1) return 1.0;
            if (ageDays >= 14) return 0.0;
            return Math.Clamp(1.0 - (ageDays - 1) / 13.0, 0, 1);
        }

        private void RecordTrend(List<LeagueCoverage> all)
        {
            try
            {
                var r = ComputeReadinessFrom(all);
                lock (_trendLock)
                {
                    _trend.Add(new CoverageSnapshot
                    {
                        TakenUtc = DateTime.UtcNow.ToString("u"),
                        OverallGdpReadiness = r.overall,
                        MarketProbabilityEngineReadiness = r.motor,
                        PremiumLeagues = all.Count(l => l.Tier == "Premium"),
                        StandardLeagues = all.Count(l => l.Tier == "Standard")
                    });
                    if (_trend.Count > 200) _trend.RemoveAt(0); // ring buffer
                }
            }
            catch { /* trend best-effort */ }
        }

        private (int overall, int motor) ComputeReadinessFrom(List<LeagueCoverage> all)
        {
            if (all.Count == 0) return (0, 0);
            var totalMatches = all.Sum(l => (double)Math.Max(1, l.MatchCount));
            var overall = (int)Math.Round(all.Sum(l => l.OverallScore * Math.Max(1, l.MatchCount)) / totalMatches);
            double num = 0, den = 0;
            foreach (var l in all)
                foreach (var c in l.Capabilities.Where(c => MotorCaps.Contains(c.Name)))
                { var w = Math.Max(1, l.MatchCount); num += c.CoverageRatio * w; den += w; }
            var motor = den > 0 ? (int)Math.Round(num / den * 100) : 0;
            return (overall, motor);
        }

        // ── Config (hardcode değil; kod yalnız makul varsayılan sağlar) ──
        private (int premium, int standard, int limited) Tiers()
        {
            var p = _config.GetValue<int?>("Coverage:Tiers:Premium") ?? 85;
            var s = _config.GetValue<int?>("Coverage:Tiers:Standard") ?? 60;
            var l = _config.GetValue<int?>("Coverage:Tiers:Limited") ?? 30;
            return (p, s, l);
        }

        private (double supported, double partial) StatusThresholds()
        {
            var s = _config.GetValue<double?>("Coverage:SupportedRatio") ?? 0.60;
            var p = _config.GetValue<double?>("Coverage:PartialRatio") ?? 0.05;
            return (s, p);
        }

        private Dictionary<string, double> CapabilityWeights()
        {
            // Config "Coverage:CapabilityWeights:<Name>" ile override edilebilir; aksi halde makul varsayılan.
            var defaults = new Dictionary<string, double>
            {
                ["Statistics"] = 0.18, ["Standings"] = 0.15, ["Prediction"] = 0.12, ["Availability"] = 0.12,
                ["Live"] = 0.10, ["Competition"] = 0.10, ["TeamProfile"] = 0.06, ["Referee"] = 0.06,
                ["Venue"] = 0.05, ["News"] = 0.03, ["Social"] = 0.02, ["Weather"] = 0.01
            };
            foreach (var key in defaults.Keys.ToList())
            {
                var ov = _config.GetValue<double?>($"Coverage:CapabilityWeights:{key}");
                if (ov.HasValue) defaults[key] = ov.Value;
            }
            return defaults;
        }
    }
}
