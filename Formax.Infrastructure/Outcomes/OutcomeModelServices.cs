using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Application.Services.Outcomes;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using HistoricalMatch = Formax.Application.Services.Outcomes.HistoricalMatch;

namespace Formax.Infrastructure.Outcomes
{
    /// <summary>
    /// TARİHSEL MAÇ AKIŞI — bitmiş maçlar başlama saatine göre sıralı ve AKIŞLA okunur (tablo belleğe varlık olarak alınmaz;
    /// yalnız 7 alanlık kompakt kayıt). Aynı fikstürün iki satırı (aynı gün, aynı ev/deplasman) bir kez işlenir.
    /// </summary>
    public sealed class OutcomeHistoryLoader
    {
        private readonly FormaxDbContext _db;
        public OutcomeHistoryLoader(FormaxDbContext db) => _db = db;

        public async Task<List<HistoricalMatch>> LoadAsync(DateTime beforeUtc, CancellationToken ct = default)
        {
            var list = new List<HistoricalMatch>(100_000);
            var seen = new HashSet<(int, int, int)>();
            var query = _db.Matches.AsNoTracking()
                .Where(m => m.Status == MatchStatuses.Finished && m.MatchDate < beforeUtc && m.HomeTeamId != m.AwayTeamId)
                .OrderBy(m => m.MatchDate).ThenBy(m => m.Id)
                .Select(m => new { m.Id, m.MatchDate, m.LeagueId, m.HomeTeamId, m.AwayTeamId, m.HomeScore, m.AwayScore })
                .AsAsyncEnumerable();
            await foreach (var m in query.WithCancellation(ct).ConfigureAwait(false))
            {
                if (m.HomeScore < 0 || m.AwayScore < 0 || m.HomeScore > 15 || m.AwayScore > 15) continue;
                var key = (m.HomeTeamId, m.AwayTeamId, (int)(m.MatchDate.Date - DateTime.UnixEpoch).TotalDays);
                if (!seen.Add(key)) continue;
                list.Add(new HistoricalMatch(m.Id, DateTime.SpecifyKind(m.MatchDate, DateTimeKind.Utc), m.LeagueId, m.HomeTeamId, m.AwayTeamId, m.HomeScore, m.AwayScore));
            }
            return list;
        }
    }

    /// <summary>
    /// MODEL EĞİTİMİ + KALİBRASYON — zamansal geriye dönük test (sızıntısız), kalibrasyon penceresinde parametre seçimi, test
    /// penceresinde yalnız raporlama. Sonuç PredictionModelRuns tablosuna yazılır; tahmin işi en son koşunun parametrelerini kullanır.
    /// </summary>
    public sealed class OutcomeModelTrainingService
    {
        public static readonly TimeSpan TestWindow = TimeSpan.FromDays(200);
        public static readonly TimeSpan CalibrationWindow = TimeSpan.FromDays(200);
        public static readonly TimeSpan TrainWindow = TimeSpan.FromDays(220);

        private readonly FormaxDbContext _db;
        private readonly OutcomeHistoryLoader _history;
        private readonly ILogger<OutcomeModelTrainingService> _log;

        public OutcomeModelTrainingService(FormaxDbContext db, OutcomeHistoryLoader history, ILogger<OutcomeModelTrainingService> log)
        {
            _db = db; _history = history; _log = log;
        }

        public static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

        public async Task<PredictionModelRun> RunAsync(DateTime nowUtc, CancellationToken ct = default)
        {
            var started = DateTime.UtcNow;
            var history = await _history.LoadAsync(nowUtc, ct).ConfigureAwait(false);
            var testStart = nowUtc - TestWindow;
            var calStart = testStart - CalibrationWindow;
            var evalStart = calStart - TrainWindow;
            var report = await Task.Run(() => OutcomeBacktest.Run(history, LockedCompetitions.All.ToHashSet(), evalStart, calStart, testStart, nowUtc), ct)
                .ConfigureAwait(false);
            var run = new PredictionModelRun
            {
                RunId = "run-" + Guid.NewGuid().ToString("N")[..20],
                ModelVersion = OutcomeModelVersion.Current,
                StartedAtUtc = started,
                CompletedAtUtc = DateTime.UtcNow,
                Status = report.TestMatches >= 100 ? "Accepted" : "Rejected",
                ParametersJson = JsonSerializer.Serialize(report.Parameters, Json),
                MetricsJson = JsonSerializer.Serialize(report, Json),
                TrainMatches = report.TrainMatches,
                CalibrationMatches = report.CalibrationMatches,
                TestMatches = report.TestMatches
            };
            _db.PredictionModelRuns.Add(run);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            _log.LogInformation("[OUTCOME MODEL] koşu {Run} {Status}: eğitim={Train} kalibrasyon={Cal} test={Test} karar={Decision} test logloss ham={Raw} kalibre={Cal2}",
                run.RunId, run.Status, report.TrainMatches, report.CalibrationMatches, report.TestMatches, report.Decision,
                report.TestRaw.CombinedLogLoss, report.TestCalibrated.CombinedLogLoss);
            return run;
        }

        public async Task<(PredictionModelRun? Run, OutcomeModelParameters Parameters)> LatestAcceptedAsync(CancellationToken ct = default)
        {
            var run = await _db.PredictionModelRuns.AsNoTracking()
                .Where(r => r.Status == "Accepted" && r.ModelVersion == OutcomeModelVersion.Current)
                .OrderByDescending(r => r.CompletedAtUtc).FirstOrDefaultAsync(ct).ConfigureAwait(false);
            var p = run == null ? new OutcomeModelParameters() : JsonSerializer.Deserialize<OutcomeModelParameters>(run.ParametersJson) ?? new OutcomeModelParameters();
            return (run, p);
        }
    }

    public sealed record SnapshotCycleReport(int Upcoming, int Written, int Unchanged, int Insufficient, string? RunId);

    /// <summary>
    /// TAHMİN SNAPSHOT İŞİ — güncel reyting durumu (şimdiye kadar bitmiş bütün maçlar) + son kalibrasyon koşusu ile yaklaşan
    /// maçların snapshot'larını üretir. Girdi değişmediyse yeni satır yazılmaz; değiştiyse yeni satır güncel olur, eskisi
    /// geçmişte kalır. Sayfa açılışı bu servisi ÇAĞIRMAZ.
    /// </summary>
    public sealed class MatchPredictionSnapshotService
    {
        public static readonly TimeSpan Horizon = TimeSpan.FromDays(8);

        private readonly FormaxDbContext _db;
        private readonly OutcomeHistoryLoader _history;
        private readonly OutcomeModelTrainingService _training;
        private readonly ILogger<MatchPredictionSnapshotService> _log;

        public MatchPredictionSnapshotService(FormaxDbContext db, OutcomeHistoryLoader history, OutcomeModelTrainingService training,
            ILogger<MatchPredictionSnapshotService> log)
        {
            _db = db; _history = history; _training = training; _log = log;
        }

        public async Task<SnapshotCycleReport> RunAsync(DateTime nowUtc, CancellationToken ct = default)
        {
            var (run, parameters) = await _training.LatestAcceptedAsync(ct).ConfigureAwait(false);
            var history = await _history.LoadAsync(nowUtc, ct).ConfigureAwait(false);
            var model = new OutcomeRatingModel(parameters);
            foreach (var m in history) model.Update(m);
            var cutoff = history.Count == 0 ? nowUtc : history[^1].KickoffUtc;

            var upcoming = await _db.Matches.AsNoTracking()
                .Where(m => (m.Status == MatchStatuses.NotStarted || m.Status == MatchStatuses.PreMatch)
                            && m.MatchDate > nowUtc && m.MatchDate <= nowUtc + Horizon && LockedCompetitions.All.Contains(m.LeagueId))
                .Select(m => new { m.Id, m.LeagueId, m.HomeTeamId, m.AwayTeamId, m.MatchDate, Home = m.HomeTeam!.Name, Away = m.AwayTeam!.Name })
                .ToListAsync(ct).ConfigureAwait(false);
            var ids = upcoming.Select(u => u.Id).ToList();
            var current = await _db.MatchPredictionSnapshots.Where(s => ids.Contains(s.MatchId) && s.IsCurrent)
                .ToListAsync(ct).ConfigureAwait(false);

            int written = 0, unchanged = 0, insufficient = 0;
            foreach (var u in upcoming)
            {
                var e = model.Expect(u.LeagueId, u.HomeTeamId, u.AwayTeamId, u.MatchDate);
                var dto = e.Sufficient
                    ? OutcomeSnapshotBuilder.Build(u.Id, OutcomePredictor.Predict(e, u.LeagueId, parameters), u.Home ?? "Ev sahibi", u.Away ?? "Deplasman")
                    : OutcomeSnapshotBuilder.Insufficient(u.Id, e, u.Home ?? "Ev sahibi", u.Away ?? "Deplasman");
                if (!e.Sufficient) insufficient++;
                var hash = InputHash(run?.RunId, e, u.Home, u.Away);
                var existing = current.Where(c => c.MatchId == u.Id).ToList();
                if (existing.Any(c => c.InputHash == hash)) { unchanged++; continue; }

                var snapshotId = "snp-" + Guid.NewGuid().ToString("N")[..24];
                dto.SnapshotId = snapshotId;
                dto.CalibrationRunId = run?.RunId;
                dto.ComputedAtUtc = nowUtc;
                dto.InputsCutoffUtc = cutoff;
                dto.ModelVersion = OutcomeModelVersion.Current;
                foreach (var c in existing) c.IsCurrent = false;
                _db.MatchPredictionSnapshots.Add(new MatchPredictionSnapshot
                {
                    SnapshotId = snapshotId, MatchId = u.Id, ModelVersion = OutcomeModelVersion.Current, CalibrationRunId = run?.RunId,
                    ComputedAtUtc = nowUtc, InputsCutoffUtc = cutoff, Status = dto.Status,
                    ExpectedHomeGoals = dto.ExpectedHomeGoals, ExpectedAwayGoals = dto.ExpectedAwayGoals,
                    EvidenceCoverage = dto.EvidenceCoverage, HomeSampleSize = e.HomeSample, AwaySampleSize = e.AwaySample,
                    PayloadJson = JsonSerializer.Serialize(dto, OutcomeModelTrainingService.Json), InputHash = hash, IsCurrent = true
                });
                written++;
            }
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            _log.LogInformation("[OUTCOME SNAPSHOT] yaklaşan={Upcoming} yazılan={Written} değişmeyen={Unchanged} yetersiz={Insufficient} koşu={Run}",
                upcoming.Count, written, unchanged, insufficient, run?.RunId);
            return new SnapshotCycleReport(upcoming.Count, written, unchanged, insufficient, run?.RunId);
        }

        public static string InputHash(string? runId, OutcomeExpectation e, string? home, string? away)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|", OutcomeModelVersion.Current, OutcomeSnapshotBuilder.SelectionVersion, runId,
                Math.Round(e.LambdaHome, 3), Math.Round(e.LambdaAway, 3), Math.Round(e.LeagueHome, 3), Math.Round(e.LeagueAway, 3),
                e.HomeSample, e.AwaySample, Math.Round(e.Coverage, 3), home, away)))).ToLowerInvariant();
    }

    /// <summary>Kullanıcı yolu okuyucusu — salt DB.</summary>
    public sealed class MatchOutcomeSnapshotReader : IMatchOutcomeSnapshotReader
    {
        private readonly FormaxDbContext _db;
        public MatchOutcomeSnapshotReader(FormaxDbContext db) => _db = db;

        public async Task<OutcomeSnapshotDto> GetCurrentAsync(int matchId, CancellationToken ct = default)
        {
            var row = await _db.MatchPredictionSnapshots.AsNoTracking()
                .Where(s => s.MatchId == matchId && s.IsCurrent).OrderByDescending(s => s.ComputedAtUtc)
                .Select(s => new { s.PayloadJson }).FirstOrDefaultAsync(ct).ConfigureAwait(false);
            return row == null ? new OutcomeSnapshotDto { MatchId = matchId, Status = "Pending" } : Parse(row.PayloadJson, matchId);
        }

        public async Task<IReadOnlyDictionary<int, OutcomeSnapshotDto>> GetCurrentForMatchesAsync(IReadOnlyCollection<int> matchIds, CancellationToken ct = default)
        {
            if (matchIds.Count == 0) return new Dictionary<int, OutcomeSnapshotDto>();
            var ids = matchIds.ToList();
            var rows = await _db.MatchPredictionSnapshots.AsNoTracking()
                .Where(s => ids.Contains(s.MatchId) && s.IsCurrent)
                .Select(s => new { s.MatchId, s.ComputedAtUtc, s.PayloadJson }).ToListAsync(ct).ConfigureAwait(false);
            return rows.GroupBy(r => r.MatchId)
                .ToDictionary(g => g.Key, g => Parse(g.OrderByDescending(x => x.ComputedAtUtc).First().PayloadJson, g.Key));
        }

        private static OutcomeSnapshotDto Parse(string json, int matchId)
            => JsonSerializer.Deserialize<OutcomeSnapshotDto>(json) ?? new OutcomeSnapshotDto { MatchId = matchId, Status = "Pending" };
    }
}
