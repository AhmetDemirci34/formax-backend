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

        /// <summary>Organizasyon adları (yalnız hazırlık maçı organizasyonlarını ayıklamak için).</summary>
        public async Task<Dictionary<int, string>> LoadCompetitionNamesAsync(CancellationToken ct = default)
        {
            var rows = await _db.Matches.AsNoTracking()
                .Where(m => m.Status == MatchStatuses.Finished)
                .GroupBy(m => m.LeagueId)
                .Select(g => new { g.Key, Name = g.Max(m => m.League) })
                .ToListAsync(ct).ConfigureAwait(false);
            return rows.ToDictionary(r => r.Key, r => r.Name ?? string.Empty);
        }
    }

    /// <summary>
    /// MODEL EĞİTİMİ + KALİBRASYON + LİG SINAVI — zamansal geriye dönük test (sızıntısız), kalibrasyon penceresinde parametre seçimi,
    /// test penceresinde raporlama ve lig bazlı uygunluk kararı. Koşu PredictionModelRuns'a, lig kararları LeaguePredictionEligibilities'e
    /// yazılır; tahmin işi en son kabul edilmiş koşunun parametrelerini ve lig kararlarını kullanır.
    /// </summary>
    public sealed class OutcomeModelTrainingService
    {
        /// <summary>
        /// Pencereler (17.09.2026): test 600 gün (kilitli liglerde ≥ 300 test maçına ulaşabilmek için), kalibrasyon 150, eğitim 150.
        /// Kilitli liglerin verisi 2024 başından başlar; eğitim penceresi bu yüzden bütün rekabetçi organizasyonları kullanır.
        /// </summary>
        public static readonly TimeSpan TestWindow = TimeSpan.FromDays(600);
        public static readonly TimeSpan CalibrationWindow = TimeSpan.FromDays(150);
        public static readonly TimeSpan TrainWindow = TimeSpan.FromDays(150);

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
            var names = await _history.LoadCompetitionNamesAsync(ct).ConfigureAwait(false);
            var testStart = nowUtc - TestWindow;
            var calStart = testStart - CalibrationWindow;
            var evalStart = calStart - TrainWindow;
            var report = await Task.Run(() =>
            {
                var catalog = CompetitionCatalog.Build(history, names);
                return OutcomeBacktest.Run(history, catalog, LockedCompetitions.All.ToHashSet(), evalStart, calStart, testStart, nowUtc, nowUtc, compareLegacy: true);
            }, ct).ConfigureAwait(false);
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
            foreach (var g in report.LeagueEligibility.Where(g => g.LeagueId != null))
            {
                _db.LeaguePredictionEligibilities.Add(new LeaguePredictionEligibility
                {
                    RunId = run.RunId, ModelVersion = run.ModelVersion, PolicyVersion = EligibilityPolicy.Version,
                    LeagueId = g.LeagueId!.Value, Status = g.Eligibility, ReasonsJson = JsonSerializer.Serialize(g.EligibilityReasons, Json),
                    TestMatches = g.Matches, ResultLogLoss = g.ResultLogLoss, BaselineResultLogLoss = g.BaselineResultLogLoss,
                    LogLossDiffCiHigh = g.LogLossDiffCiHigh, CalibrationError = g.CalibrationError, HomeBias = g.HomeBias, DrawBias = g.DrawBias,
                    MetricsJson = JsonSerializer.Serialize(g, Json), EvaluatedAtUtc = run.CompletedAtUtc
                });
            }
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            _log.LogInformation("[OUTCOME MODEL] koşu {Run} {Status}: eğitim={Train} kalibrasyon={Cal} test={Test} karar={Decision} 1X2 logloss kalibre={Cal2} önceki={Prev} taban={Base} uygunluk={Elig}",
                run.RunId, run.Status, report.TrainMatches, report.CalibrationMatches, report.TestMatches, report.Decision,
                report.TestCalibrated.ResultLogLoss, report.TestPreviousModel?.ResultLogLoss, report.TestLeagueBaseline.ResultLogLoss,
                string.Join(",", report.LeagueEligibility.Select(g => g.LeagueId + ":" + g.Eligibility)));
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

        public async Task<Dictionary<int, (string Status, List<string> Reasons)>> EligibilityAsync(string? runId, CancellationToken ct = default)
        {
            if (runId == null) return new();
            var rows = await _db.LeaguePredictionEligibilities.AsNoTracking().Where(e => e.RunId == runId)
                .Select(e => new { e.LeagueId, e.Status, e.ReasonsJson }).ToListAsync(ct).ConfigureAwait(false);
            return rows.ToDictionary(r => r.LeagueId, r => (r.Status, JsonSerializer.Deserialize<List<string>>(r.ReasonsJson) ?? new List<string>()));
        }
    }

    public sealed record SnapshotCycleReport(int Upcoming, int Written, int Unchanged, int Insufficient, string? RunId)
    {
        public int NeedsReview { get; init; }
        public Dictionary<string, int> Eligibility { get; init; } = new();
    }

    public sealed record RecomputeTrigger(int MatchId, string TriggerType, string TriggerSource, DateTime TriggeredAtUtc);

    public sealed record RecomputeOutcome(int MatchId, string Outcome, string? SnapshotId);

    /// <summary>
    /// TAHMİN SNAPSHOT İŞİ — güncel reyting durumu (şimdiye kadar bitmiş bütün maçlar) + son kalibrasyon koşusu + lig uygunluk kararları
    /// ile yaklaşan maçların snapshot'larını üretir. Girdi (reyting, uygunluk, doğrulanmış maç zekâsı) değişmediyse yeni satır yazılmaz;
    /// değiştiyse yeni satır yazılır, değişim kapısından geçerse güncel olur (eskisi silinmez), geçmezse NeedsReview olarak saklanır ve
    /// eski yayımlanmış snapshot kullanıcıda kalır. Sayfa açılışı bu servisi ÇAĞIRMAZ.
    /// </summary>
    public sealed class MatchPredictionSnapshotService
    {
        public static readonly TimeSpan Horizon = TimeSpan.FromDays(8);
        /// <summary>Periyodik tur ve kuyruk işçisi aynı anda snapshot yazmaz (tek süreç).</summary>
        public static readonly SemaphoreSlim WriteLock = new(1, 1);

        private static readonly string[] Scheduled = { MatchStatuses.NotStarted, MatchStatuses.PreMatch };

        private readonly FormaxDbContext _db;
        private readonly OutcomeHistoryLoader _history;
        private readonly OutcomeModelTrainingService _training;
        private readonly ILogger<MatchPredictionSnapshotService> _log;

        public MatchPredictionSnapshotService(FormaxDbContext db, OutcomeHistoryLoader history, OutcomeModelTrainingService training,
            ILogger<MatchPredictionSnapshotService> log)
        {
            _db = db; _history = history; _training = training; _log = log;
        }

        private sealed class ModelContext
        {
            public PredictionModelRun? Run;
            public OutcomeModelParameters Parameters = new();
            public Dictionary<int, (string Status, List<string> Reasons)> Eligibility = new();
            public OutcomeRatingModel Model = null!;
            public DateTime Cutoff;
            public Dictionary<int, List<DateTime>> TeamMatches = new();
            public Dictionary<int, List<DateTime>> CompetitionMatches = new();
        }

        private async Task<ModelContext> BuildContextAsync(DateTime nowUtc, CancellationToken ct)
        {
            var ctx = new ModelContext();
            (ctx.Run, ctx.Parameters) = await _training.LatestAcceptedAsync(ct).ConfigureAwait(false);
            ctx.Eligibility = await _training.EligibilityAsync(ctx.Run?.RunId, ct).ConfigureAwait(false);
            var history = await _history.LoadAsync(nowUtc, ct).ConfigureAwait(false);
            var names = await _history.LoadCompetitionNamesAsync(ct).ConfigureAwait(false);
            await Task.Run(() =>
            {
                var catalog = CompetitionCatalog.Build(history, names);
                ctx.Model = new OutcomeRatingModel(ctx.Parameters, catalog);
                foreach (var m in history)
                {
                    ctx.Model.Update(m);
                    Add(ctx.TeamMatches, m.HomeTeamId, m.KickoffUtc);
                    Add(ctx.TeamMatches, m.AwayTeamId, m.KickoffUtc);
                    Add(ctx.CompetitionMatches, m.LeagueId, m.KickoffUtc);
                }
                ctx.Model.RefitLeagueStrengths(nowUtc);
            }, ct).ConfigureAwait(false);
            ctx.Cutoff = history.Count == 0 ? nowUtc : history[^1].KickoffUtc;
            return ctx;
        }

        private static void Add(Dictionary<int, List<DateTime>> map, int team, DateTime at)
        {
            if (!map.TryGetValue(team, out var l)) map[team] = l = new List<DateTime>();
            l.Add(at);
        }

        public async Task<SnapshotCycleReport> RunAsync(DateTime nowUtc, CancellationToken ct = default)
        {
            await WriteLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var ctx = await BuildContextAsync(nowUtc, ct).ConfigureAwait(false);
                var upcomingIds = await _db.Matches.AsNoTracking()
                    .Where(m => Scheduled.Contains(m.Status) && m.MatchDate > nowUtc && m.MatchDate <= nowUtc + Horizon && LockedCompetitions.All.Contains(m.LeagueId))
                    .Select(m => m.Id).ToListAsync(ct).ConfigureAwait(false);
                // Güncel (yayımlanmış) snapshot'ı olan ama artık oynanmayacak (erteleme/iptal) maçlar da yeniden değerlendirilir → Disabled.
                var unscheduled = await (from s in _db.MatchPredictionSnapshots.AsNoTracking()
                                         join m in _db.Matches.AsNoTracking() on s.MatchId equals m.Id
                                         where s.IsCurrent && m.MatchDate > nowUtc.AddDays(-2) && !Scheduled.Contains(m.Status)
                                               && m.Status != MatchStatuses.Finished && m.Status != MatchStatuses.Live
                                               && s.PredictionEligibility != PredictionEligibilities.Disabled
                                         select m.Id).ToListAsync(ct).ConfigureAwait(false);
                var triggers = upcomingIds.Concat(unscheduled).Distinct()
                    .Select(id => new RecomputeTrigger(id, "Periodic", "outcome-model-job", nowUtc)).ToList();
                var outcomes = await WriteAsync(ctx, triggers, nowUtc, ct).ConfigureAwait(false);
                var report = new SnapshotCycleReport(upcomingIds.Count, outcomes.Count(o => o.Outcome.StartsWith("Published")), outcomes.Count(o => o.Outcome.StartsWith("Unchanged")),
                    outcomes.Count(o => o.Outcome == "Published:Disabled"), ctx.Run?.RunId)
                {
                    NeedsReview = outcomes.Count(o => o.Outcome == "NeedsReview"),
                    Eligibility = outcomes.GroupBy(o => o.Outcome).ToDictionary(g => g.Key, g => g.Count())
                };
                _log.LogInformation("[OUTCOME SNAPSHOT] yaklaşan={Upcoming} sonuçlar={Outcomes} koşu={Run}",
                    upcomingIds.Count, string.Join(",", report.Eligibility.Select(k => k.Key + "=" + k.Value)), ctx.Run?.RunId);
                return report;
            }
            finally { WriteLock.Release(); }
        }

        /// <summary>Kuyruk işçisi — yalnız verilen maçlar (doğrulanmış olay sonrası).</summary>
        public async Task<IReadOnlyList<RecomputeOutcome>> RecomputeAsync(IReadOnlyList<RecomputeTrigger> triggers, DateTime nowUtc, CancellationToken ct = default)
        {
            if (triggers.Count == 0) return Array.Empty<RecomputeOutcome>();
            await WriteLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var ctx = await BuildContextAsync(nowUtc, ct).ConfigureAwait(false);
                return await WriteAsync(ctx, triggers, nowUtc, ct).ConfigureAwait(false);
            }
            finally { WriteLock.Release(); }
        }

        private async Task<List<RecomputeOutcome>> WriteAsync(ModelContext ctx, IReadOnlyList<RecomputeTrigger> triggers, DateTime nowUtc, CancellationToken ct)
        {
            var ids = triggers.Select(t => t.MatchId).Distinct().ToList();
            var matches = await _db.Matches.AsNoTracking().Where(m => ids.Contains(m.Id))
                .Select(m => new { m.Id, m.LeagueId, m.HomeTeamId, m.AwayTeamId, m.MatchDate, m.Status, Home = m.HomeTeam!.Name, Away = m.AwayTeam!.Name })
                .ToDictionaryAsync(m => m.Id, ct).ConfigureAwait(false);
            var current = await _db.MatchPredictionSnapshots.Where(s => ids.Contains(s.MatchId) && s.IsCurrent).ToListAsync(ct).ConfigureAwait(false);
            var fingerprints = await FingerprintsAsync(ids, ct).ConfigureAwait(false);
            var results = new List<RecomputeOutcome>();

            foreach (var t in triggers.GroupBy(x => x.MatchId).Select(g => g.OrderBy(x => x.TriggeredAtUtc).Last()))
            {
                if (!matches.TryGetValue(t.MatchId, out var u)) { results.Add(new(t.MatchId, "Skipped:MatchMissing", null)); continue; }
                if (u.MatchDate <= nowUtc) { results.Add(new(t.MatchId, "Skipped:KickoffPassed", null)); continue; } // başlamış maçın tahmini değişmez
                if (!LockedCompetitions.IsLocked(u.LeagueId)) { results.Add(new(t.MatchId, "Skipped:OutOfScope", null)); continue; }
                var (fpHash, fpInputs) = fingerprints.GetValueOrDefault(u.Id, (Hash(u.Status + "|" + u.MatchDate.ToString("O")), new List<string>()));
                if (!fingerprints.ContainsKey(u.Id)) fpHash = Hash(u.Status + "|" + u.MatchDate.ToString("O"));

                var e = ctx.Model.Expect(u.LeagueId, u.HomeTeamId, u.AwayTeamId, u.MatchDate);
                var home = u.Home ?? "Ev sahibi"; var away = u.Away ?? "Deplasman";
                var matchGates = e.GateReasons.ToList();
                if (!Scheduled.Contains(u.Status)) matchGates.Add("MATCH_NOT_SCHEDULED");
                OutcomeSnapshotDto dto;
                IReadOnlyList<string> outputGates = Array.Empty<string>();
                if (e.Sufficient)
                {
                    var pr = OutcomePredictor.Predict(e, u.LeagueId, ctx.Parameters);
                    dto = OutcomeSnapshotBuilder.Build(u.Id, pr, home, away);
                    outputGates = OutcomePredictor.OutputGates(pr, ctx.Parameters);
                }
                else dto = OutcomeSnapshotBuilder.Insufficient(u.Id, e, home, away);

                var league = ctx.Eligibility.TryGetValue(u.LeagueId, out var le) ? le : ((string Status, List<string> Reasons)?)null;
                var (eligibility, reasons) = OutcomeSnapshotBuilder.Combine(league?.Status, league?.Reasons ?? new List<string>(), matchGates, outputGates);
                dto.PredictionEligibility = eligibility;
                dto.EligibilityReasons = reasons;
                if (eligibility != PredictionEligibilities.Enabled) dto.Notice = OutcomeSnapshotBuilder.NotEligibleNotice;
                dto.Strength = new OutcomeStrengthDto
                {
                    CrossLeague = e.CrossLeague, HomeLeagueId = e.HomeLeagueId, AwayLeagueId = e.AwayLeagueId,
                    HomeLeagueStrength = e.HomeLeagueStrength, AwayLeagueStrength = e.AwayLeagueStrength,
                    HomeLeagueLinks = e.HomeLeagueLinks, AwayLeagueLinks = e.AwayLeagueLinks,
                    HomeClubRating = e.HomeClubRating, AwayClubRating = e.AwayClubRating,
                    LambdaHome = Math.Round(e.LambdaHome, 4), LambdaAway = Math.Round(e.LambdaAway, 4), EloHomeExpectation = Math.Round(e.EloHomeExpectation, 4),
                    LeagueHome = Math.Round(e.LeagueHome, 5), LeagueAway = Math.Round(e.LeagueAway, 5)
                };
                AnalysisConsistencyValidator.ValidateCardReasons(dto);

                var hash = InputHash(ctx.Run?.RunId, e, home, away, eligibility, fpHash);
                var existing = current.Where(c => c.MatchId == u.Id).ToList();
                if (existing.Any(c => c.InputHash == hash)) { results.Add(new(u.Id, "Unchanged", existing.First(c => c.InputHash == hash).SnapshotId)); continue; }
                if (await _db.MatchPredictionSnapshots.AnyAsync(s => s.MatchId == u.Id && s.InputHash == hash && s.PublicationStatus == "NeedsReview", ct).ConfigureAwait(false))
                { results.Add(new(u.Id, "Unchanged:NeedsReviewExists", null)); continue; }

                var snapshotId = "snp-" + Guid.NewGuid().ToString("N")[..24];
                var prev = existing.OrderByDescending(c => c.ComputedAtUtc).FirstOrDefault();
                OutcomeSnapshotDto? prevDto = prev == null ? null : Parse(prev.PayloadJson, u.Id);
                var trigger = t.TriggerType;
                if (prevDto != null && trigger == "Periodic" && (prevDto.ModelVersion != OutcomeModelVersion.Current || prev!.CalibrationRunId != ctx.Run?.RunId)) trigger = "ModelUpdate";
                dto.SnapshotId = snapshotId;
                dto.CalibrationRunId = ctx.Run?.RunId;
                dto.ComputedAtUtc = nowUtc;
                dto.InputsCutoffUtc = ctx.Cutoff;
                dto.ModelVersion = OutcomeModelVersion.Current;
                dto.TriggerType = trigger;
                dto.PreviousSnapshotId = prev?.SnapshotId;
                foreach (var c in dto.MainCards.Concat(dto.Families.SelectMany(f => f.Items))) { c.SnapshotId = snapshotId; c.ModelVersion = dto.ModelVersion; }

                var newMatches = prevDto?.InputsCutoffUtc is DateTime pc
                    ? CountSince(ctx.TeamMatches, u.HomeTeamId, pc) + CountSince(ctx.TeamMatches, u.AwayTeamId, pc) : 0;
                var newCompetition = prevDto?.InputsCutoffUtc is DateTime pcc ? CountSince(ctx.CompetitionMatches, u.LeagueId, pcc) : 0;
                var inputs = new List<string>(fpInputs);
                if (newMatches > 0) inputs.Add($"NewFinishedMatches:{newMatches}");
                if (prevDto?.PredictionEligibility != eligibility) inputs.Add($"Eligibility:{prevDto?.PredictionEligibility ?? "none"}->{eligibility}");
                OutcomeChangeAudit audit;
                if (prevDto != null && prevDto.Status == "Available" && dto.Status == "Available")
                {
                    if (newCompetition > 0) inputs.Add($"NewCompetitionMatches:{newCompetition}");
                    audit = OutcomeChangeGate.Evaluate(prevDto, dto, e, u.LeagueId, ctx.Parameters, newMatches, trigger, t.TriggerSource, t.TriggeredAtUtc, inputs, newCompetition);
                }
                else
                    audit = new OutcomeChangeAudit
                    {
                        PreviousSnapshotId = prev?.SnapshotId, TriggerType = trigger, TriggerSource = t.TriggerSource, TriggeredAtUtc = t.TriggeredAtUtc,
                        PreviousModelVersion = prevDto?.ModelVersion, CalibrationRunId = ctx.Run?.RunId, PreviousCalibrationRunId = prevDto?.CalibrationRunId,
                        NewFinishedMatches = newMatches, OldEvidenceCoverage = prevDto?.EvidenceCoverage ?? 0, NewEvidenceCoverage = dto.EvidenceCoverage,
                        OldEligibility = prevDto?.PredictionEligibility, NewEligibility = eligibility, NewInputs = inputs,
                        DecisionReason = prev == null ? "FIRST_SNAPSHOT" : "NO_COMPARABLE_PROBABILITIES"
                    };
                var published = audit.Decision == "Published";

                var row = new MatchPredictionSnapshot
                {
                    SnapshotId = snapshotId, MatchId = u.Id, ModelVersion = OutcomeModelVersion.Current, CalibrationRunId = ctx.Run?.RunId,
                    ComputedAtUtc = nowUtc, InputsCutoffUtc = ctx.Cutoff, Status = dto.Status,
                    ExpectedHomeGoals = dto.ExpectedHomeGoals, ExpectedAwayGoals = dto.ExpectedAwayGoals,
                    EvidenceCoverage = dto.EvidenceCoverage, HomeSampleSize = e.HomeSample, AwaySampleSize = e.AwaySample,
                    PayloadJson = JsonSerializer.Serialize(dto, OutcomeModelTrainingService.Json), InputHash = hash, IsCurrent = published,
                    PredictionEligibility = eligibility, EligibilityReasonsJson = JsonSerializer.Serialize(reasons, OutcomeModelTrainingService.Json),
                    PublicationStatus = audit.Decision, PreviousSnapshotId = prev?.SnapshotId, TriggerType = trigger, TriggerSource = t.TriggerSource,
                    TriggeredAtUtc = t.TriggeredAtUtc, ChangeAuditJson = JsonSerializer.Serialize(audit, OutcomeModelTrainingService.Json),
                    IntelligenceFingerprint = fpHash, SelectionVersion = OutcomeSnapshotBuilder.SelectionVersion, KickoffUtc = u.MatchDate
                };
                if (published) foreach (var c in existing) c.IsCurrent = false;
                _db.MatchPredictionSnapshots.Add(row);
                if (!published)
                    await AddDiagnosticAsync(u.Id, snapshotId, "SNAPSHOT_NEEDS_REVIEW",
                        JsonSerializer.Serialize(audit.Lines.Where(l => l.Exceeded), OutcomeModelTrainingService.Json), $"review:{u.Id}:{hash[..16]}", nowUtc, ct).ConfigureAwait(false);
                results.Add(new(u.Id, published ? "Published" + (eligibility == PredictionEligibilities.Enabled ? "" : ":" + eligibility) : "NeedsReview", snapshotId));
            }
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            return results;
        }

        private static int CountSince(Dictionary<int, List<DateTime>> map, int team, DateTime since)
            => map.TryGetValue(team, out var l) ? l.Count(d => d > since) : 0;

        private async Task AddDiagnosticAsync(int matchId, string? snapshotId, string kind, string detail, string dedupe, DateTime nowUtc, CancellationToken ct)
        {
            if (_db.PredictionDiagnostics.Local.Any(d => d.DedupeKey == dedupe)) return;
            if (await _db.PredictionDiagnostics.AnyAsync(d => d.DedupeKey == dedupe, ct).ConfigureAwait(false)) return;
            _db.PredictionDiagnostics.Add(new PredictionDiagnostic
            {
                MatchId = matchId, SnapshotId = snapshotId, Kind = kind, Detail = detail.Length > 2000 ? detail[..2000] : detail,
                DedupeKey = dedupe, CreatedAtUtc = nowUtc
            });
        }

        /// <summary>
        /// DOĞRULANMIŞ MAÇ ZEKÂSI PARMAK İZİ — yalnız resmî kaynaktan doğrulanmış yapılandırılmış veriler: resmî ilk 11 (kaynak içerik
        /// özeti), doğrulanmış kritik gelişmeler (erteleme/iptal/askı/saat/stat), maç durumu ve başlama saati. Sıradan haber, yorum ya da
        /// resmî olmayan kadro/sakatlık listesi parmak izine GİRMEZ (tahmini yenilemez).
        /// </summary>
        private async Task<Dictionary<int, (string Hash, List<string> Inputs)>> FingerprintsAsync(List<int> ids, CancellationToken ct)
        {
            var matches = await _db.Matches.AsNoTracking().Where(m => ids.Contains(m.Id))
                .Select(m => new { m.Id, m.Status, m.MatchDate }).ToListAsync(ct).ConfigureAwait(false);
            var lineups = await _db.MatchLineups.AsNoTracking()
                .Where(l => ids.Contains(l.MatchId) && l.Provider != null && l.Provider.StartsWith("official:") && (l.VerificationStatus == "Verified" || l.VerificationStatus == "PartiallyVerified"))
                .Select(l => new { l.MatchId, l.VerificationStatus, l.RawContentHash, l.HomeLineupsReleased, l.AwayLineupsReleased })
                .ToListAsync(ct).ConfigureAwait(false);
            var devs = await _db.MatchCriticalDevelopments.AsNoTracking()
                .Where(d => ids.Contains(d.MatchId) && d.VerificationStatus == "Verified")
                .Select(d => new { d.MatchId, d.DevelopmentType, d.EvidenceHash }).ToListAsync(ct).ConfigureAwait(false);
            var map = new Dictionary<int, (string, List<string>)>();
            foreach (var m in matches)
            {
                var inputs = new List<string>();
                var parts = new List<string> { "status:" + m.Status, "kickoff:" + m.MatchDate.ToString("yyyy-MM-ddTHH:mm") };
                foreach (var l in lineups.Where(l => l.MatchId == m.Id))
                {
                    parts.Add($"lineup:{l.VerificationStatus}:{l.HomeLineupsReleased}:{l.AwayLineupsReleased}:{l.RawContentHash}");
                    inputs.Add($"OfficialLineup:{l.VerificationStatus}");
                }
                foreach (var d in devs.Where(d => d.MatchId == m.Id).OrderBy(d => d.EvidenceHash))
                {
                    parts.Add($"critical:{d.DevelopmentType}:{d.EvidenceHash}");
                    inputs.Add($"Critical:{d.DevelopmentType}");
                }
                map[m.Id] = (Hash(string.Join("|", parts)), inputs);
            }
            return map;
        }

        private static string Hash(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();

        private static OutcomeSnapshotDto Parse(string json, int matchId)
            => JsonSerializer.Deserialize<OutcomeSnapshotDto>(json) ?? new OutcomeSnapshotDto { MatchId = matchId, Status = "Pending" };

        public static string InputHash(string? runId, OutcomeExpectation e, string? home, string? away)
            => InputHash(runId, e, home, away, null, null);

        public static string InputHash(string? runId, OutcomeExpectation e, string? home, string? away, string? eligibility, string? fingerprint)
            => Hash(string.Join("|", OutcomeModelVersion.Current, OutcomeSnapshotBuilder.SelectionVersion, EligibilityPolicy.Version, runId,
                Math.Round(e.LambdaHome, 3), Math.Round(e.LambdaAway, 3), Math.Round(e.LeagueHome, 3), Math.Round(e.LeagueAway, 3),
                e.HomeSample, e.AwaySample, Math.Round(e.Coverage, 3), string.Join(",", e.GateReasons), home, away, eligibility, fingerprint));
    }

    /// <summary>
    /// KALICI YENİLEME KUYRUĞU YAZICISI — doğrulanmış olayı yazan servis, AYNI DbContext işleminde çağırır (olay yazılırsa istek de
    /// yazılır). Aynı olay (DedupeKey) ikinci kez eklenmez. Debounce: istek <see cref="Debounce"/> sonra işlenir.
    /// </summary>
    public static class PredictionRecomputeQueue
    {
        public static readonly TimeSpan Debounce = TimeSpan.FromMinutes(2);

        public static async Task<bool> EnqueueAsync(FormaxDbContext db, int matchId, string triggerType, string triggerSource, string dedupeKey,
            DateTime nowUtc, CancellationToken ct = default)
        {
            dedupeKey = dedupeKey.Length > 160 ? dedupeKey[..160] : dedupeKey;
            if (db.PredictionRecomputeRequests.Local.Any(r => r.DedupeKey == dedupeKey)) return false;
            if (await db.PredictionRecomputeRequests.AnyAsync(r => r.DedupeKey == dedupeKey, ct).ConfigureAwait(false)) return false;
            db.PredictionRecomputeRequests.Add(new PredictionRecomputeRequest
            {
                MatchId = matchId, TriggerType = triggerType.Length > 32 ? triggerType[..32] : triggerType,
                TriggerSource = triggerSource.Length > 80 ? triggerSource[..80] : triggerSource,
                DedupeKey = dedupeKey, RequestedAtUtc = nowUtc, DueAtUtc = nowUtc + Debounce, Status = "Pending"
            });
            return true;
        }
    }

    /// <summary>Kuyruk işçisinin tur raporu.</summary>
    public sealed record RecomputeCycleReport(int Claimed, IReadOnlyList<RecomputeOutcome> Outcomes, int ScorecardsLocked, int ScorecardsSettled);

    /// <summary>
    /// KUYRUK İŞÇİSİ + CANLI KARNE — zamanı gelen yenileme isteklerini DB kilidiyle alır (süresi dolan kilit restart sonrası devralınır),
    /// maç başına tek yeniden hesaplama yapar; başlama anında son yayımlanmış snapshot'ı karneye kilitler, maç bitince sonuç botunun
    /// kanonik sonucuyla değerlendirir.
    /// </summary>
    public sealed class PredictionRecomputeWorker
    {
        public static readonly TimeSpan Lock = TimeSpan.FromMinutes(5);
        public const int MaxAttempts = 5;

        private readonly FormaxDbContext _db;
        private readonly MatchPredictionSnapshotService _snapshots;
        private readonly ILogger<PredictionRecomputeWorker> _log;

        public PredictionRecomputeWorker(FormaxDbContext db, MatchPredictionSnapshotService snapshots, ILogger<PredictionRecomputeWorker> log)
        {
            _db = db; _snapshots = snapshots; _log = log;
        }

        public async Task<RecomputeCycleReport> RunOnceAsync(DateTime nowUtc, CancellationToken ct = default)
        {
            var due = await _db.PredictionRecomputeRequests
                .Where(r => (r.Status == "Pending" || r.Status == "Processing") && r.DueAtUtc <= nowUtc && (r.LockedUntilUtc == null || r.LockedUntilUtc < nowUtc))
                .OrderBy(r => r.DueAtUtc).Take(100).ToListAsync(ct).ConfigureAwait(false);
            IReadOnlyList<RecomputeOutcome> outcomes = Array.Empty<RecomputeOutcome>();
            if (due.Count > 0)
            {
                foreach (var r in due) { r.Status = "Processing"; r.LockedUntilUtc = nowUtc + Lock; r.Attempts++; }
                await _db.SaveChangesAsync(ct).ConfigureAwait(false);
                try
                {
                    outcomes = await _snapshots.RecomputeAsync(due.Select(r => new RecomputeTrigger(r.MatchId, r.TriggerType, r.TriggerSource, r.RequestedAtUtc)).ToList(), nowUtc, ct).ConfigureAwait(false);
                    var byMatch = outcomes.ToDictionary(o => o.MatchId);
                    foreach (var r in due)
                    {
                        var o = byMatch.GetValueOrDefault(r.MatchId);
                        r.Status = o == null ? "Failed" : o.Outcome.StartsWith("Skipped") || o.Outcome.StartsWith("Unchanged") ? "Skipped" : "Done";
                        r.Outcome = o?.Outcome ?? "NoOutcome";
                        r.ResultSnapshotId = o?.SnapshotId;
                        r.ProcessedAtUtc = nowUtc;
                        r.LockedUntilUtc = null;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    foreach (var r in due)
                    {
                        r.Status = r.Attempts >= MaxAttempts ? "Failed" : "Pending";
                        r.Outcome = (ex.GetType().Name + ": " + ex.Message) is var msg && msg.Length > 200 ? msg[..200] : ex.GetType().Name + ": " + ex.Message;
                        r.LockedUntilUtc = null;
                        r.DueAtUtc = nowUtc.AddMinutes(2 * r.Attempts);
                    }
                    _log.LogError(ex, "[PREDICTION QUEUE] yeniden hesaplama başarısız");
                }
                await _db.SaveChangesAsync(ct).ConfigureAwait(false);
                _log.LogInformation("[PREDICTION QUEUE] alınan={Count} sonuç={Outcomes}", due.Count, string.Join(",", outcomes.Select(o => o.MatchId + ":" + o.Outcome)));
            }
            var (locked, settled) = await ScorecardsAsync(nowUtc, ct).ConfigureAwait(false);
            return new RecomputeCycleReport(due.Count, outcomes, locked, settled);
        }

        public async Task<(int Locked, int Settled)> ScorecardsAsync(DateTime nowUtc, CancellationToken ct)
        {
            // 1) Kilitle — başlama saati gelmiş, karnesi olmayan kilitli lig maçları: başlamadan ÖNCE hesaplanmış son yayımlanmış snapshot.
            var since = nowUtc.AddDays(-3);
            var candidates = await (from m in _db.Matches.AsNoTracking()
                                    where m.MatchDate <= nowUtc && m.MatchDate >= since && LockedCompetitions.All.Contains(m.LeagueId)
                                          && !_db.PredictionScorecards.Any(s => s.MatchId == m.Id)
                                    select new { m.Id, m.LeagueId, m.MatchDate }).ToListAsync(ct).ConfigureAwait(false);
            var locked = 0;
            foreach (var c in candidates)
            {
                var snap = await _db.MatchPredictionSnapshots.AsNoTracking()
                    .Where(s => s.MatchId == c.Id && s.PublicationStatus != "NeedsReview" && s.ComputedAtUtc < c.MatchDate)
                    .OrderByDescending(s => s.ComputedAtUtc).FirstOrDefaultAsync(ct).ConfigureAwait(false);
                if (snap == null) continue;
                var dto = JsonSerializer.Deserialize<OutcomeSnapshotDto>(snap.PayloadJson) ?? new OutcomeSnapshotDto();
                _db.PredictionScorecards.Add(new PredictionScorecard
                {
                    MatchId = c.Id, SnapshotId = snap.SnapshotId, ModelVersion = snap.ModelVersion, LeagueId = c.LeagueId,
                    Eligibility = snap.PredictionEligibility ?? dto.PredictionEligibility, PredictionCreatedAtUtc = snap.ComputedAtUtc,
                    KickoffUtc = c.MatchDate, LockedAtUtc = nowUtc,
                    MainCardsJson = JsonSerializer.Serialize(dto.MainCards.Select(x => new { x.Family, x.Market, x.MarketKey, x.Probability, x.CalibratedProbability }), OutcomeModelTrainingService.Json),
                    ProbabilitiesJson = JsonSerializer.Serialize(OutcomeChangeGate.Probabilities(dto), OutcomeModelTrainingService.Json)
                });
                locked++;
            }
            if (locked > 0) await _db.SaveChangesAsync(ct).ConfigureAwait(false);

            // 2) Değerlendir — sonuç botunun kanonik final sonucu.
            // NOT: join içindeki AsNoTracking bütün sorguyu izlenmez yapar → karne değişiklikleri kaydedilmezdi; burada izlenir.
            var open = await (from s in _db.PredictionScorecards
                              join m in _db.Matches on s.MatchId equals m.Id
                              where s.SettledAtUtc == null && (m.Status == MatchStatuses.Finished || m.Status == MatchStatuses.Postponed || m.Status == MatchStatuses.Cancelled)
                              select new { s, m.Status, m.HomeScore, m.AwayScore }).ToListAsync(ct).ConfigureAwait(false);
            foreach (var o in open)
            {
                var s = o.s;
                s.FinalStatus = o.Status;
                s.SettledAtUtc = nowUtc;
                if (o.Status != MatchStatuses.Finished) continue;
                s.FinalHomeScore = o.HomeScore; s.FinalAwayScore = o.AwayScore;
                var cards = JsonSerializer.Deserialize<List<ScorecardCard>>(s.MainCardsJson) ?? new List<ScorecardCard>();
                bool? Hit(string family) => cards.FirstOrDefault(c => c.Family == family) is { } c ? OutcomeBacktest.Hit(c.MarketKey, o.HomeScore, o.AwayScore) : null;
                s.ResultCardCorrect = Hit(OutcomeFamilies.Result);
                s.GoalsCardCorrect = Hit(OutcomeFamilies.Goals);
                s.BttsCardCorrect = Hit(OutcomeFamilies.Btts);
                var probs = JsonSerializer.Deserialize<Dictionary<string, double>>(s.ProbabilitiesJson) ?? new Dictionary<string, double>();
                var key = o.HomeScore > o.AwayScore ? "Ev Sahibi Kazanır" : o.HomeScore == o.AwayScore ? "Beraberlik" : "Deplasman Kazanır";
                if (probs.TryGetValue(key, out var p)) s.ResultLogLoss = Math.Round(-Math.Log(Math.Max(1e-6, p)), 5);
            }
            if (open.Count > 0) await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            return (locked, open.Count);
        }

        private sealed class ScorecardCard
        {
            public string Family { get; set; } = string.Empty;
            public string Market { get; set; } = string.Empty;
            public string? MarketKey { get; set; }
            public int Probability { get; set; }
        }
    }

    /// <summary>Kullanıcı yolu okuyucusu — salt DB. Enabled olmayan snapshot'ta yüzdeler taşınmaz.</summary>
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
        {
            var dto = JsonSerializer.Deserialize<OutcomeSnapshotDto>(json) ?? new OutcomeSnapshotDto { MatchId = matchId, Status = "Pending" };
            // 3.0 öncesi snapshot'larda uygunluk yoktu → yayımlanmaz (lig sınavından geçmemiş).
            if (dto.ModelVersion != OutcomeModelVersion.Current) dto.PredictionEligibility = PredictionEligibilities.Disabled;
            return OutcomeSnapshotBuilder.ForUser(dto);
        }
    }
}
