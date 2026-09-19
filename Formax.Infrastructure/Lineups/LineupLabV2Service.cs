using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.Lineups;
using Formax.Application.Services.Lineups.V2;
using Formax.Application.Services.Outcomes;
using Formax.Domain.Constants;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Outcomes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using HistoricalMatch = Formax.Application.Services.Outcomes.HistoricalMatch;

namespace Formax.Infrastructure.Lineups
{
    /// <summary>
    /// PLAYER IMPACT V2 LABORATUVARI — veri yükleme + taban dondurma + deney çalıştırma.
    ///
    /// YALNIZ DB OKUR: hiçbir dış web isteği atmaz, API-Football'a çıkmaz, hiçbir şey YAZMAZ.
    /// Kullanıcı istek yolunda çalışmaz; ölçüm koşucusundan ya da admin teşhis ucundan tetiklenir.
    /// </summary>
    public sealed class LineupLabV2Service
    {
        /// <summary>Ana araştırma ligleri — geçmiş mevki VE dakika verisi olanlar.</summary>
        public static readonly IReadOnlyList<int> PrimaryLeagues = new[] { 39, 135 };

        /// <summary>Yalnız duyarlılık deneyi için (mevki/dakika eksik) — ana sonuca GİRMEZ.</summary>
        public const int SensitivityOnlyLeague = 203;

        private readonly FormaxDbContext _db;
        private readonly OutcomeHistoryLoader _history;
        private readonly LineupHistoryLoader _lineups;
        private readonly OutcomeModelTrainingService _training;
        private readonly ILogger<LineupLabV2Service> _log;

        public LineupLabV2Service(
            FormaxDbContext db, OutcomeHistoryLoader history, LineupHistoryLoader lineups,
            OutcomeModelTrainingService training, ILogger<LineupLabV2Service> log)
        {
            _db = db; _history = history; _lineups = lineups; _training = training; _log = log;
        }

        /// <summary>Laboratuvarın girdi kümesi — bir kez kurulur, bütün adaylar bunu paylaşır.</summary>
        public sealed record LabDataset(
            IReadOnlyList<LabSample> Samples,
            IReadOnlyList<LabFold> Folds,
            string ConfigHash,
            OutcomeModelParameters Parameters,
            string? RunId,
            int LineupObservations,
            int UsableFeatureMatches,
            IReadOnlyDictionary<string, int> Notes);

        /// <summary>
        /// Veri kümesini kurar: tarihsel akıştan TABANI dondurur, kadroları yükler, beklenen 11'i
        /// ve özellikleri YALNIZ geçmişten üretir.
        /// </summary>
        public async Task<LabDataset> BuildDatasetAsync(
            DateTime nowUtc, IReadOnlyList<int>? leagues = null, CancellationToken ct = default)
        {
            var scope = (leagues ?? PrimaryLeagues).ToHashSet();
            var history = await _history.LoadAsync(nowUtc, ct).ConfigureAwait(false);
            var names = await _history.LoadCompetitionNamesAsync(ct).ConfigureAwait(false);
            var (run, parameters) = await _training.LatestAcceptedAsync(ct).ConfigureAwait(false);
            var observations = await _lineups.LoadAsync(null, officialOnly: true, ct).ConfigureAwait(false);

            // Kadro gözlemleri — YALNIZ doğrulama kuralını geçenler ve kapsam ligleri.
            var verified = observations
                .Where(o => scope.Contains(o.LeagueId) && LineupVerificationRule.Check(o).Accepted)
                .OrderBy(o => o.KickoffUtc).ThenBy(o => o.MatchId)
                .ToList();

            // Gerçek dakika ve diziliş — kadro başlığı/oyuncu satırından (dış istek yok).
            var ids = verified.Select(o => o.MatchId).ToList();
            var quality = await _db.MatchLineups.AsNoTracking()
                .Where(l => ids.Contains(l.MatchId))
                .Select(l => new { l.MatchId, l.DataQuality, l.HomeFormation, l.AwayFormation })
                .ToDictionaryAsync(l => l.MatchId, ct).ConfigureAwait(false);
            var minutes = (await _db.MatchLineupPlayers.AsNoTracking()
                    .Where(p => ids.Contains(p.MatchId) && p.MinutesPlayed != null)
                    .Select(p => new { p.MatchId, p.Side, p.PlayerName, p.MinutesPlayed })
                    .ToListAsync(ct).ConfigureAwait(false))
                .GroupBy(p => p.MatchId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var notes = new Dictionary<string, int>();
            var samples = new List<LabSample>();

            // ── TABANI DONDUR: tarihsel akış bir kez, kickoff sırasına göre ────────────
            var catalog = CompetitionCatalog.Build(history, names);
            var model = new OutcomeRatingModel(parameters, catalog);
            var byMatch = verified.ToDictionary(o => o.MatchId);

            // Takım geçmişi: beklenen 11'i kurmak için (yalnız kesimden önceki maçlar kullanılır).
            var teamHistory = new Dictionary<int, List<ExpectedLineupBuilder.TeamMatchLineup>>();
            var lastStarters = new Dictionary<int, HashSet<string>>();
            var lastFormation = new Dictionary<int, string?>();

            foreach (var m in history.OrderBy(h => h.KickoffUtc).ThenBy(h => h.MatchId))
            {
                var expectation = model.Expect(m.LeagueId, m.HomeTeamId, m.AwayTeamId, m.KickoffUtc);

                if (scope.Contains(m.LeagueId) && byMatch.TryGetValue(m.MatchId, out var obs) && expectation.Sufficient)
                {
                    var q = quality.GetValueOrDefault(m.MatchId);
                    var minuteRows = minutes.GetValueOrDefault(m.MatchId) ?? new();

                    var homeSlots = Slots(obs, home: true, minuteRows);
                    var awaySlots = Slots(obs, home: false, minuteRows);

                    // BEKLENEN 11 — kesim maçın başlama anı; aynı maç ASLA kendi beklentisine girmez.
                    var homeExpected = ExpectedLineupBuilder.Build(m.HomeTeamId, m.KickoffUtc,
                        teamHistory.GetValueOrDefault(m.HomeTeamId) ?? new());
                    var awayExpected = ExpectedLineupBuilder.Build(m.AwayTeamId, m.KickoffUtc,
                        teamHistory.GetValueOrDefault(m.AwayTeamId) ?? new());

                    var features = new LineupMatchFeatures(m.MatchId, m.KickoffUtc, m.LeagueId,
                        LineupFeatureExtractor.Extract(homeSlots, homeExpected,
                            lastStarters.GetValueOrDefault(m.HomeTeamId), q?.HomeFormation,
                            lastFormation.GetValueOrDefault(m.HomeTeamId), q?.DataQuality ?? "StartersOnly"),
                        LineupFeatureExtractor.Extract(awaySlots, awayExpected,
                            lastStarters.GetValueOrDefault(m.AwayTeamId), q?.AwayFormation,
                            lastFormation.GetValueOrDefault(m.AwayTeamId), q?.DataQuality ?? "StartersOnly"));

                    var prediction = OutcomePredictor.Predict(expectation, m.LeagueId, parameters);
                    samples.Add(new LabSample(m.MatchId, m.KickoffUtc, m.LeagueId, expectation,
                        m.HomeGoals, m.AwayGoals, prediction.Calibrated, prediction.Baseline,
                        features.Usable ? features : null));
                    if (!features.Usable) Bump(notes, "beklenen_kadro_kurulamadi");

                    // Takım geçmişini maç İŞLENDİKTEN SONRA güncelle → sonraki maçlar için kullanılır.
                    Append(teamHistory, m.HomeTeamId, new ExpectedLineupBuilder.TeamMatchLineup(m.MatchId, m.KickoffUtc, m.HomeTeamId, homeSlots));
                    Append(teamHistory, m.AwayTeamId, new ExpectedLineupBuilder.TeamMatchLineup(m.MatchId, m.KickoffUtc, m.AwayTeamId, awaySlots));
                    lastStarters[m.HomeTeamId] = homeSlots.Where(s => s.Starter).Select(s => s.PlayerKey).ToHashSet(StringComparer.Ordinal);
                    lastStarters[m.AwayTeamId] = awaySlots.Where(s => s.Starter).Select(s => s.PlayerKey).ToHashSet(StringComparer.Ordinal);
                    lastFormation[m.HomeTeamId] = q?.HomeFormation;
                    lastFormation[m.AwayTeamId] = q?.AwayFormation;
                }

                model.Update(m);   // taban reyting, maçın SONUCUNU ancak tahminden SONRA görür
            }

            var folds = BuildFolds(samples);
            var configHash = Hash(string.Join("|",
                LineupLabVersion.Current, OutcomeModelVersion.Current, run?.RunId,
                string.Join(",", scope.OrderBy(x => x)),
                ExpectedLineupBuilder.HalfLifeMatches, ExpectedLineupBuilder.MinTeamMatches,
                LineupPoissonAdjuster.MaxLogDelta, LineupLabV2.MinTrainingRows,
                string.Join(",", LineupLabV2.RidgeGrid),
                string.Join(",", folds.Select(f => $"{f.Name}:{f.StartUtc:yyyyMMdd}-{f.EndUtc:yyyyMMdd}:{f.IsHoldout}"))));

            _log.LogInformation(
                "[LINEUP LAB V2] veri kümesi: örnek={Samples} kadrolu={Usable} fold={Folds} config={Hash}",
                samples.Count, samples.Count(s => s.Features is { Usable: true }), folds.Count, configHash[..12]);

            return new LabDataset(samples, folds, configHash, parameters, run?.RunId,
                verified.Count, samples.Count(s => s.Features is { Usable: true }), notes);
        }

        /// <summary>
        /// FOLD TAKVİMİ — sezon sınırlarından. İlk sezon yalnız ÖĞRENME (fold değil); orta sezon
        /// yürüyen doğrulama fold'ları; en son sezon DOKUNULMAMIŞ holdout.
        /// </summary>
        public static IReadOnlyList<LabFold> BuildFolds(IReadOnlyList<LabSample> samples)
        {
            if (samples.Count == 0) return Array.Empty<LabFold>();
            var withLineup = samples.Where(s => s.Features is { Usable: true }).ToList();
            if (withLineup.Count == 0) return Array.Empty<LabFold>();

            // Sezon = Temmuz–Haziran. En eski sezon öğrenmeye ayrılır.
            static int Season(DateTime d) => d.Month >= 7 ? d.Year : d.Year - 1;
            var seasons = withLineup.Select(s => Season(s.KickoffUtc)).Distinct().OrderBy(x => x).ToList();
            if (seasons.Count < 2) return Array.Empty<LabFold>();

            var folds = new List<LabFold>();
            var validationSeasons = seasons.Skip(1).Take(Math.Max(0, seasons.Count - 2)).ToList();
            foreach (var season in validationSeasons)
            {
                // Sezonu çeyreklere böl: yürüyen doğrulama.
                var start = new DateTime(season, 7, 1, 0, 0, 0, DateTimeKind.Utc);
                for (var q = 0; q < 4; q++)
                {
                    var from = start.AddMonths(q * 3);
                    var to = start.AddMonths((q + 1) * 3);
                    if (withLineup.Any(s => s.KickoffUtc >= from && s.KickoffUtc < to))
                        folds.Add(new LabFold($"V{season}Q{q + 1}", from, to, IsHoldout: false));
                }
            }
            var holdoutSeason = seasons[^1];
            var holdoutStart = new DateTime(holdoutSeason, 7, 1, 0, 0, 0, DateTimeKind.Utc);
            folds.Add(new LabFold($"HOLDOUT{holdoutSeason}", holdoutStart, holdoutStart.AddYears(1), IsHoldout: true));
            return folds;
        }

        private static List<ExpectedLineupBuilder.LineupSlot> Slots(
            MatchLineupObservation obs, bool home,
            IReadOnlyList<dynamic> minuteRows)
        {
            var side = home ? "Home" : "Away";
            var minuteByName = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var r in minuteRows)
            {
                string rowSide = r.Side; string name = r.PlayerName; int? played = r.MinutesPlayed;
                if (!string.Equals(rowSide, side, StringComparison.OrdinalIgnoreCase) || played == null) continue;
                minuteByName[name] = played.Value;
            }
            return obs.Side(home)
                .Where(p => !string.IsNullOrEmpty(p.PlayerKey))
                .Select(p => new ExpectedLineupBuilder.LineupSlot(
                    p.PlayerKey, p.Position, p.Starter,
                    minuteByName.TryGetValue(p.DisplayName, out var m) ? m : null))
                .ToList();
        }

        private static void Append(Dictionary<int, List<ExpectedLineupBuilder.TeamMatchLineup>> map, int teamId,
            ExpectedLineupBuilder.TeamMatchLineup row)
        {
            if (!map.TryGetValue(teamId, out var list)) map[teamId] = list = new();
            list.Add(row);
        }

        private static void Bump(Dictionary<string, int> notes, string key)
            => notes[key] = notes.GetValueOrDefault(key) + 1;

        private static string Hash(string s)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();
    }
}
