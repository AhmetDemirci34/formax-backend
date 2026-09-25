using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.Outcomes;
using Formax.Domain.Constants;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Outcomes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// GERÇEK VERİ LABORATUVARI (25.09.2026) — yayın politikasını üretime almadan önce gerçek DB üzerinde ölçer.
///
/// YALNIZ OKUR: DB'ye tek satır yazmaz; durum makinesi bellekte çalışır (dry-run). Dış istek yok. Ortam değişkeni
/// <c>FORMAX_LAB_SQL</c> (bağlantı dizesi) yoksa atlanır; <c>FORMAX_LAB_OUT</c> verilirse rapor oraya yazılır.
///
/// Ölçülenler: (B) 19.09 üretim matrisinin yeniden üretimi, beş pencerenin ham kapı sonucu, determinizm, Serie A H4
/// (lig başına düşük skor ρ — yalnız ölçüm) ve (L) beş pencerenin tekrar oynatılmasıyla durum geçmişi.
/// </summary>
public class EligibilityPublicationLabTests
{
    public static readonly DateTime[] ReplayCutoffs =
    {
        new(2026, 8, 14, 2, 0, 0, DateTimeKind.Utc),
        new(2026, 8, 28, 2, 0, 0, DateTimeKind.Utc),
        new(2026, 9, 11, 2, 0, 0, DateTimeKind.Utc),
        new(2026, 9, 18, 2, 0, 0, DateTimeKind.Utc),
        new(2026, 9, 25, 2, 0, 0, DateTimeKind.Utc),
    };

    private sealed class CountingInterceptor : DbCommandInterceptor
    {
        public int Commands;
        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        { Interlocked.Increment(ref Commands); return result; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        { Interlocked.Increment(ref Commands); return ValueTask.FromResult(result); }
        public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
        { Interlocked.Increment(ref Commands); return result; }
        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
        { Interlocked.Increment(ref Commands); return ValueTask.FromResult(result); }
        public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
        { Interlocked.Increment(ref Commands); return result; }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        { Interlocked.Increment(ref Commands); return ValueTask.FromResult(result); }
    }

    private static readonly Dictionary<int, string> Names = new()
    {
        [39] = "Premier League", [40] = "Championship", [140] = "La Liga", [135] = "Serie A", [78] = "Bundesliga", [61] = "Ligue 1",
        [203] = "Süper Lig", [88] = "Eredivisie", [2] = "UCL", [3] = "UEL", [848] = "UECL"
    };

    [SkippableFact]
    public async Task Lab_GercekVeri_BesPencere_DryRunReplay()
    {
        var conn = Environment.GetEnvironmentVariable("FORMAX_LAB_SQL");
        Skip.If(string.IsNullOrWhiteSpace(conn), "FORMAX_LAB_SQL yok — gerçek veri laboratuvarı atlandı");
        var counter = new CountingInterceptor();
        await using var db = new FormaxDbContext(new DbContextOptionsBuilder<FormaxDbContext>()
            .UseSqlServer(conn, o => o.CommandTimeout(120)).AddInterceptors(counter).Options);
        var sb = new StringBuilder();
        var json = new Dictionary<string, object?>();
        var total = Stopwatch.StartNew();

        // ── 1) 19.09 üretim matrisi (run-b33c…) ──
        const string prodRun = "run-b33c3494787b4898a615";
        var prodRows = await db.LeagueMarketEligibilities.AsNoTracking().Where(e => e.RunId == prodRun).ToListAsync();
        var prodOpen = prodRows.Where(r => r.Status == MarketEligibilityStatuses.Eligible).Select(r => (r.LeagueId, r.Family)).OrderBy(x => x.LeagueId).ThenBy(x => x.Family).ToList();
        var prodMetrics = await db.PredictionModelRuns.AsNoTracking().Where(r => r.RunId == prodRun).Select(r => r.MetricsJson).FirstAsync();
        var prodCutoff = DateTime.SpecifyKind(JsonDocument.Parse(prodMetrics).RootElement.GetProperty("TestEndUtc").GetDateTime(), DateTimeKind.Utc);
        sb.AppendLine($"## B1 — 19.09 üretim matrisi {prodRun} (kesim {prodCutoff:O}): {prodOpen.Count} hücre");
        sb.AppendLine(string.Join(", ", prodOpen.Select(x => $"{Names[x.LeagueId]} {x.Family}")));
        json["prodOpen"] = prodOpen.Select(x => $"{x.LeagueId}:{x.Family}").ToList();

        // ── 2) Geçmiş bir kez yüklenir ──
        var loader = new OutcomeHistoryLoader(db);
        var q0 = counter.Commands;
        var loadSw = Stopwatch.StartNew();
        var full = await loader.LoadAsync(ReplayCutoffs[^1]);
        var names = await loader.LoadCompetitionNamesAsync();
        json["historyLoadMs"] = loadSw.ElapsedMilliseconds;
        json["historyRows"] = full.Count;
        json["sqlCommandsHistoryLoad"] = counter.Commands - q0;
        var hash = BacktestEligibilityEvaluationSource.CurrentConfigHash;

        IReadOnlyList<CellEvaluation> Eval(DateTime cutoff)
            => BacktestEligibilityEvaluationSource.Evaluate(full.Where(m => m.KickoffUtc < cutoff).ToList(), names, cutoff, cutoff, hash);

        // ── 3) Üretim matrisinin yeniden üretimi (aynı kesim) ──
        var repro = (await loader.LoadAsync(prodCutoff)) is var h19 ? BacktestEligibilityEvaluationSource.Evaluate(h19, names, prodCutoff, prodCutoff, hash) : null!;
        var reproOpen = repro.Where(e => e.GateStatus == MarketEligibilityStatuses.Eligible).Select(e => (e.OrganizationId, e.MarketFamily)).OrderBy(x => x.OrganizationId).ThenBy(x => x.MarketFamily).ToList();
        var mism = prodRows.Count(r => repro.FirstOrDefault(e => e.OrganizationId == r.LeagueId && e.MarketFamily == r.Family)?.GateStatus != r.Status);
        sb.AppendLine($"\nB1b — aynı kesimle yeniden üretim: {reproOpen.Count} Eligible hücre; 19.09 satırlarıyla statü uyuşmazlığı = {mism}/{prodRows.Count}");
        json["reproOpen"] = reproOpen.Select(x => $"{x.OrganizationId}:{x.MarketFamily}").ToList();
        json["reproMismatch"] = mism;

        // ── 4) Beş pencere (Base) + determinizm ──
        var perCutoff = new Dictionary<DateTime, IReadOnlyList<CellEvaluation>>();
        var perfMs = new List<long>();
        var procPeak = 0L;
        foreach (var c in ReplayCutoffs)
        {
            var sw = Stopwatch.StartNew();
            perCutoff[c] = Eval(c);
            perfMs.Add(sw.ElapsedMilliseconds);
            procPeak = Math.Max(procPeak, Process.GetCurrentProcess().PeakWorkingSet64);
        }
        var deterministic = true;
        foreach (var c in ReplayCutoffs)
        {
            var again = Eval(c);
            var a = JsonSerializer.Serialize(perCutoff[c].OrderBy(e => e.OrganizationId).ThenBy(e => e.MarketFamily).Select(e => new { e.OrganizationId, e.MarketFamily, e.RawGateStatus, e.GateStatus, e.SampleCount, e.LogLoss, e.ConfidenceIntervalHigh, e.ConfidenceIntervalLow, e.Ece, e.Bias, e.CalibrationSlope }));
            var b = JsonSerializer.Serialize(again.OrderBy(e => e.OrganizationId).ThenBy(e => e.MarketFamily).Select(e => new { e.OrganizationId, e.MarketFamily, e.RawGateStatus, e.GateStatus, e.SampleCount, e.LogLoss, e.ConfidenceIntervalHigh, e.ConfidenceIntervalLow, e.Ece, e.Bias, e.CalibrationSlope }));
            if (a != b) deterministic = false;
        }
        json["evaluationMsPerCutoff"] = perfMs;
        json["deterministic"] = deterministic;
        json["peakWorkingSetBytes"] = procPeak;
        sb.AppendLine($"\n## B2 — beş pencere ham kapı (Base 4.0). Determinizm (2 koşu, bütün alanlar): {(deterministic ? "AYNI" : "FARKLI")}");
        sb.AppendLine("| Organizasyon | Market | " + string.Join(" | ", ReplayCutoffs.Select(c => c.ToString("dd.MM"))) + " | PASS/5 |");
        sb.AppendLine("|---|---|" + string.Join("", ReplayCutoffs.Select(_ => "---|")) + "---|");
        foreach (var cell in perCutoff[ReplayCutoffs[^1]].Select(e => e.Cell).Distinct().OrderBy(x => x.Item1).ThenBy(x => x.Item2))
        {
            var row = ReplayCutoffs.Select(c => perCutoff[c].FirstOrDefault(e => e.Cell == cell)).ToList();
            sb.AppendLine($"| {Names.GetValueOrDefault(cell.Item1, cell.Item1.ToString())} | {cell.Item2} | " +
                          string.Join(" | ", row.Select(e => e == null ? "-" : e.RawGateStatus == RawGateStatuses.Pass ? "PASS" : e.RawGateStatus == RawGateStatuses.HardFail ? "HARD" : "fail")) +
                          $" | {row.Count(e => e?.IsPass == true)}/5 |");
        }
        json["raw"] = ReplayCutoffs.ToDictionary(c => c.ToString("yyyy-MM-dd"), c => perCutoff[c].Select(e => new
        {
            e.OrganizationId, e.MarketFamily, e.RawGateStatus, e.GateStatus, e.RawGateReasons, e.SampleCount, e.LogLoss, e.BaselineLogLoss,
            e.DifferenceFromBaseline, ciLow = e.ConfidenceIntervalLow, ciHigh = e.ConfidenceIntervalHigh, e.Ece, e.Bias, e.Coverage, e.CalibrationSlope, e.CalibrationIntercept
        }).ToList());

        // ── 5) Serie A H4 (lig başına düşük skor ρ) — YALNIZ ÖLÇÜM, üretime alınmaz ──
        sb.AppendLine("\n## B3 — Serie A 1X2: Base vs H4 (LowScoreRho, yalnız ölçüm)");
        sb.AppendLine("| Kesim | Base statü | Base logloss | H4 statü | H4 logloss | H4−Base |");
        sb.AppendLine("|---|---|---|---|---|---|");
        var h4 = new List<object>();
        foreach (var c in ReplayCutoffs)
        {
            var hist = full.Where(m => m.KickoffUtc < c).ToList();
            var testStart = c - OutcomeModelTrainingService.TestWindow;
            var calStart = testStart - OutcomeModelTrainingService.CalibrationWindow;
            var evalStart = calStart - OutcomeModelTrainingService.TrainWindow;
            var r = OutcomeBacktest.Run(hist, CompetitionCatalog.Build(hist, names), LockedCompetitions.All.ToHashSet(), evalStart, calStart, testStart, c, c,
                compareLegacy: false, candidate: true, leagueCalibration: OutcomeBacktest.LeagueCalibrationAxes.LowScoreRho);
            var h = r.MarketEligibility.First(m => m.LeagueId == LockedCompetitions.SerieA && m.Family == MarketFamilies.MatchResult);
            var b = perCutoff[c].First(e => e.OrganizationId == LockedCompetitions.SerieA && e.MarketFamily == MarketFamilies.MatchResult);
            sb.AppendLine($"| {c:dd.MM} | {b.GateStatus} | {b.LogLoss:0.00000} | {h.Status} | {h.LogLoss:0.00000} | {h.LogLoss - b.LogLoss:+0.00000;-0.00000} |");
            h4.Add(new { cutoff = c, baseStatus = b.GateStatus, baseLogLoss = b.LogLoss, h4Status = h.Status, h4LogLoss = h.LogLoss });
        }
        json["serieAH4"] = h4;

        // ── 6) L — tekrar oynatma (dry-run, bellek): başlangıç = şu an yayında olan matris ──
        var pinned = await (from s in db.MatchPredictionSnapshots.AsNoTracking()
                            where s.IsCurrent && s.ModelVersion == OutcomeModelVersion.Current && s.CalibrationRunId != null
                            group s by s.CalibrationRunId into g
                            orderby g.Count() descending, g.Key
                            select g.Key).FirstOrDefaultAsync();
        var allSame = true;
        var anchor = DateTime.SpecifyKind(await db.PredictionModelRuns.AsNoTracking().Where(r => r.RunId == pinned).Select(r => r.CompletedAtUtc).FirstAsync(), DateTimeKind.Utc);
        foreach (var mode in new[] { "from-bootstrap", "chronological" })
        {
            var anchorFor = mode == "from-bootstrap" ? anchor : (DateTime?)null;
            ReplayTable(mode, anchorFor);
        }

        void ReplayTable(string mode, DateTime? anchorUtc)
        {
        sb.AppendLine($"\n## L [{mode}] — tekrar oynatma (dry-run). Bootstrap kaynağı (güncel snapshot'ların koşusu): {pinned}" +
                      (anchorUtc == null ? "" : $"; çapa {anchorUtc:O} (öncesi yalnız kanıt geçmişi)"));
        List<string> Replay(out Dictionary<(int, string), string> final, out List<(DateTime, StateTransition)> log)
        {
            var states = new Dictionary<(int, string), string>();
            foreach (var l in LockedCompetitions.All)
                foreach (var f in MarketFamilies.All)
                    states[(l, f)] = prodRows.Any(r => r.LeagueId == l && r.Family == f && r.Status == MarketEligibilityStatuses.Eligible && r.RunId == pinned)
                        ? PublishedStates.Open : PublishedStates.Closed;
            var history = new Dictionary<(int, string), List<CellEvaluation>>();
            log = new List<(DateTime, StateTransition)>();
            foreach (var c in ReplayCutoffs)
                foreach (var t in anchorUtc != null && c < anchorUtc.Value
                             ? EligibilityPublicationPolicy.RecordHistory(states, history, perCutoff[c])
                             : EligibilityPublicationPolicy.Apply(states, history, perCutoff[c])) log.Add((c, t));
            final = states;
            return log.Select(x => $"{x.Item1:O}|{x.Item2.OrganizationId}|{x.Item2.MarketFamily}|{x.Item2.Before}|{x.Item2.After}|{x.Item2.Reason}").ToList();
        }
        var first = Replay(out var finalStates, out var tlog);
        var second = Replay(out _, out _);
        var sameHistory = first.SequenceEqual(second);
        sb.AppendLine($"Aynı giriş → aynı durum geçmişi: {(sameHistory ? "EVET" : "HAYIR")}");
        sb.AppendLine("| Organizasyon | Market | " + string.Join(" | ", ReplayCutoffs.Select(c => c.ToString("dd.MM"))) + " | Son durum |");
        sb.AppendLine("|---|---|" + string.Join("", ReplayCutoffs.Select(_ => "---|")) + "---|");
        foreach (var cell in finalStates.Keys.OrderBy(x => x.Item1).ThenBy(x => x.Item2))
        {
            var steps = ReplayCutoffs.Select(c => tlog.FirstOrDefault(x => x.Item1 == c && x.Item2.OrganizationId == cell.Item1 && x.Item2.MarketFamily == cell.Item2).Item2).ToList();
            sb.AppendLine($"| {Names.GetValueOrDefault(cell.Item1)} | {cell.Item2} | " + string.Join(" | ", steps.Select((t, i) => t == null ? "-" :
                $"{(t.Before == t.After ? "" : Short(t.Before) + "→")}{Short(t.After)} ({(perCutoff[ReplayCutoffs[i]].First(e => e.Cell == cell).RawGateStatus switch { RawGateStatuses.Pass => "P", RawGateStatuses.HardFail => "H", _ => "F" })})")) +
                $" | **{finalStates[cell]}** |");
        }
        json["replay_" + mode] = tlog.Select(x => new { cutoff = x.Item1, x.Item2.OrganizationId, x.Item2.MarketFamily, x.Item2.Before, x.Item2.After, x.Item2.Reason }).ToList();
        json["finalStates_" + mode] = finalStates.ToDictionary(k => k.Key.Item1 + ":" + k.Key.Item2, k => k.Value);
        json["sameHistory_" + mode] = sameHistory;
        allSame &= sameHistory;
        }
        json["sqlCommandsTotal"] = counter.Commands;
        json["totalMs"] = total.ElapsedMilliseconds;
        sb.AppendLine($"\nSQL komutu (toplam, lab): {counter.Commands}; geçmiş yükleme {json["historyLoadMs"]} ms, {full.Count} satır; kesim başına değerlendirme ms: {string.Join(", ", perfMs)}; tepe bellek {procPeak / 1048576} MB");

        var outDir = Environment.GetEnvironmentVariable("FORMAX_LAB_OUT");
        if (!string.IsNullOrWhiteSpace(outDir))
        {
            Directory.CreateDirectory(outDir);
            await File.WriteAllTextAsync(Path.Combine(outDir, "eligibility-lab.md"), sb.ToString());
            await File.WriteAllTextAsync(Path.Combine(outDir, "eligibility-lab.json"), JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true }));
        }
        Assert.True(deterministic);
        Assert.True(allSame);
    }

    private static string Short(string s) => s switch
    {
        PublishedStates.Open => "Open", PublishedStates.Closed => "Closed", PublishedStates.PendingOpen => "PendOpen", PublishedStates.PendingClose => "PendClose", _ => s
    };
}
