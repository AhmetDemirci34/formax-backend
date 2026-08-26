using System.Globalization;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Predictions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

// Gerçek üretim servisini, gerçek üretim veritabanına karşı çalıştırır.
// Bu runner hiçbir tahmin mantığı içermez - yalnız ShadowPredictionService'i çağırır.
var root = FindRepoRoot(AppContext.BaseDirectory);
var conn = Arg("--conn") ?? "Server=localhost\\SQLEXPRESS;Database=FormaxDB;Trusted_Connection=True;TrustServerCertificate=True";
var horizon = int.TryParse(Arg("--days"), out var d) ? d : 8;
var outDir = Arg("--out") ?? Path.Combine(root, "FORMAX_PROBABILITY_ENGINE", "shadow_mode_v1");
var command = args.Length > 0 ? args[0].ToLowerInvariant() : "help";

var services = new ServiceCollection();
services.AddLogging(b => b.SetMinimumLevel(LogLevel.Information));
services.AddDbContext<FormaxDbContext>(o => o.UseSqlServer(conn).EnableSensitiveDataLogging(false));
services.AddSingleton(new PredictionEngineOptions { EngineRoot = root });
services.AddScoped<ShadowPredictionService>();
services.AddScoped<PredictionSettlementService>();
var provider = services.BuildServiceProvider();

switch (command)
{
    case "predict": return await Predict();
    case "settle": return await Settle();
    case "report": return await Report();
    case "multiinstance": return await Formax.ShadowRunner.MultiInstance.RunAsync(root, conn, outDir, horizon);
    case "integrity": { using var isc = provider.CreateScope(); return await Formax.ShadowRunner.Integrity.RunAsync(isc.ServiceProvider.GetRequiredService<FormaxDbContext>(), outDir); }
    case "hosttest": return await Formax.ShadowRunner.HostLifecycle.RunAsync(root, conn, outDir);
    case "eftest": { using var es = provider.CreateScope(); return await Formax.ShadowRunner.EfTriggerProbe.RunAsync(es.ServiceProvider.GetRequiredService<FormaxDbContext>(), outDir); }
    case "verify": { using var vs = provider.CreateScope(); return await Formax.ShadowRunner.Verify.RunAsync(vs.ServiceProvider.GetRequiredService<FormaxDbContext>(), outDir); }
    case "regress": return Formax.ShadowRunner.Regression.Run(new PredictionEngineOptions { EngineRoot = root }, root);
    case "all": { var g = Formax.ShadowRunner.Regression.Run(new PredictionEngineOptions { EngineRoot = root }, root); if (g != 0) return g; var a = await Predict(); if (a != 0) return a; await Settle(); return await Report(); }
    default:
        Console.WriteLine("FormaxShadowRunner - runs the PRODUCTION shadow prediction service against the real database");
        Console.WriteLine("  predict   generate predictions for upcoming real matches (writes to Predictions)");
        Console.WriteLine("  settle    attach results of finished matches (writes to PredictionSettlements)");
        Console.WriteLine("  report    shadow_predictions.csv + shadow_summary.csv from what is in the database");
        Console.WriteLine("  regress   production code path vs model_validation_v2 reference");
        Console.WriteLine("  all       regress, predict, settle, report");
        return 0;
}

string? Arg(string name)
{
    for (var i = 0; i < args.Length - 1; i++)
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
    return null;
}

static string FindRepoRoot(string start)
{
    var dir = new DirectoryInfo(start);
    while (dir is not null)
    {
        if (Directory.Exists(Path.Combine(dir.FullName, "FORMAX_HISTORICAL_MASTER"))) return dir.FullName;
        dir = dir.Parent;
    }
    return Directory.GetCurrentDirectory();
}

async Task<int> Predict()
{
    using var scope = provider.CreateScope();
    var svc = scope.ServiceProvider.GetRequiredService<ShadowPredictionService>();

    Console.WriteLine("== FORMAX PRODUCTION PREDICTION — SHADOW MODE ==");
    var report = await svc.RunCycleAsync(horizon);

    Console.WriteLine();
    Console.WriteLine($"model fingerprint     : {report.ModelFingerprint}");
    Console.WriteLine($"rating evidence through: {report.RatingEvidenceThrough:yyyy-MM-dd}");
    Console.WriteLine($"upcoming matches seen : {report.UpcomingMatchesSeen}");
    Console.WriteLine($"predicted             : {report.Predicted}  (accepted {report.Accepted} / rejected {report.Rejected})");
    Console.WriteLine($"inserted              : {report.Inserted}");
    Console.WriteLine($"already published     : {report.AlreadyPublished}");
    Console.WriteLine($"immutability refused  : {report.ImmutabilityRefused}");
    Console.WriteLine($"failed                : {report.Failed}");
    Console.WriteLine($"cycle elapsed         : {report.ElapsedMs} ms");

    if (report.PerMatchLatencyMs.Count > 0)
    {
        var sorted = report.PerMatchLatencyMs.OrderBy(x => x).ToList();
        var avg = sorted.Average();
        var p95 = sorted[(int)Math.Min(sorted.Count - 1, Math.Floor(0.95 * (sorted.Count - 1)))];
        Console.WriteLine($"per-match latency     : avg {avg:0.00} ms   p95 {p95:0.00} ms   max {sorted[^1]:0.00} ms");
    }
    return report.Failed == 0 ? 0 : 3;
}

async Task<int> Settle()
{
    using var scope = provider.CreateScope();
    var svc = scope.ServiceProvider.GetRequiredService<PredictionSettlementService>();
    var r = await svc.SettleAsync(5000);
    Console.WriteLine();
    Console.WriteLine($"SETTLEMENT: candidates {r.Candidates}, settled {r.Settled}, no result yet {r.NoResultYet}, " +
                      $"already settled {r.AlreadySettled}, rejected as early {r.RejectedAsEarly}, failed {r.Failed} ({r.ElapsedMs} ms)");
    return r.Failed == 0 ? 0 : 3;
}

async Task<int> Report()
{
    using var scope = provider.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<FormaxDbContext>();
    Directory.CreateDirectory(outDir);

    var rows = await (
        from p in db.Predictions.AsNoTracking()
        join m in db.Matches.AsNoTracking() on p.MatchId equals m.Id
        join h in db.Teams.AsNoTracking() on m.HomeTeamId equals h.Id
        join a in db.Teams.AsNoTracking() on m.AwayTeamId equals a.Id
        join s in db.PredictionSettlements.AsNoTracking() on p.PredictionId equals s.PredictionId into sj
        from s in sj.DefaultIfEmpty()
        orderby p.Sequence
        select new
        {
            p.Sequence, p.PredictionId, p.MatchId, p.CanonicalMatchId, p.MatchDate, p.PredictionTimestamp,
            p.EvidenceCutoff, p.ModelVersion, p.TeamStrengthVersion, p.GateVersion, p.CalibrationVersion,
            p.HomeProbability, p.DrawProbability, p.AwayProbability, p.PredictionEligible,
            p.ConfidenceClass, p.GateStatus, p.GateReason, p.ContentHash, p.ShadowMode,
            m.LeagueId, m.League, m.Round, m.Status,
            HomeTeam = h.Name, AwayTeam = a.Name,
            ActualHomeGoals = (int?)(s == null ? null : s.ActualHomeGoals),
            ActualAwayGoals = (int?)(s == null ? null : s.ActualAwayGoals),
            ActualResult = s == null ? null : s.ActualResult,
            SettlementTimestamp = (DateTime?)(s == null ? null : s.SettlementTimestamp)
        }).ToListAsync();

    static string N(double? v) => v.HasValue ? v.Value.ToString("0.00000000", CultureInfo.InvariantCulture) : "";
    static string Q(string? s) => s is null ? "" : (s.Contains(',') || s.Contains('"')
        ? "\"" + s.Replace("\"", "\"\"") + "\"" : s);

    var predPath = Path.Combine(outDir, "shadow_predictions.csv");
    using (var w = new StreamWriter(predPath, false))
    {
        w.WriteLine("Sequence,PredictionId,ProductionMatchId,CanonicalMatchId,MatchDate,PredictionTimestamp,EvidenceCutoff," +
                    "ModelVersion,TeamStrengthVersion,GateVersion,CalibrationVersion," +
                    "HomeProbability,DrawProbability,AwayProbability,ProbabilitySum," +
                    "PredictionEligible,ConfidenceClass,GateStatus,GateReason,ContentHash,ShadowMode," +
                    "LeagueId,League,Round,CompetitionType,MatchStatus,HomeTeam,AwayTeam," +
                    "ActualHomeGoals,ActualAwayGoals,ActualResult,SettlementTimestamp,LogLoss");
        foreach (var r in rows)
        {
            var comp = CanonicalIdentityResolver.ResolveCompetition(r.LeagueId);
            var ctype = comp is null ? "" : CanonicalIdentityResolver.ResolveCompetitionType(comp, r.Round);
            double? sum = r.PredictionEligible ? (r.HomeProbability ?? 0) + (r.DrawProbability ?? 0) + (r.AwayProbability ?? 0) : null;
            double? ll = null;
            if (r.PredictionEligible && r.ActualResult is not null)
            {
                var p = r.ActualResult == "HomeWin" ? r.HomeProbability!.Value
                      : r.ActualResult == "Draw" ? r.DrawProbability!.Value : r.AwayProbability!.Value;
                ll = -Math.Log(Math.Max(p, 1e-15));
            }
            w.WriteLine(string.Join(',',
                r.Sequence.ToString(CultureInfo.InvariantCulture), r.PredictionId,
                r.MatchId.ToString(CultureInfo.InvariantCulture), Q(r.CanonicalMatchId),
                r.MatchDate.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
                r.PredictionTimestamp.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
                r.EvidenceCutoff?.ToString("yyyy-MM-dd") ?? "",
                Q(r.ModelVersion), Q(r.TeamStrengthVersion), Q(r.GateVersion), Q(r.CalibrationVersion),
                N(r.HomeProbability), N(r.DrawProbability), N(r.AwayProbability), N(sum),
                r.PredictionEligible ? "True" : "False", Q(r.ConfidenceClass), Q(r.GateStatus), Q(r.GateReason),
                Q(r.ContentHash), r.ShadowMode ? "True" : "False",
                r.LeagueId.ToString(CultureInfo.InvariantCulture), Q(r.League), Q(r.Round), Q(ctype), Q(r.Status),
                Q(r.HomeTeam), Q(r.AwayTeam),
                r.ActualHomeGoals?.ToString(CultureInfo.InvariantCulture) ?? "",
                r.ActualAwayGoals?.ToString(CultureInfo.InvariantCulture) ?? "",
                Q(r.ActualResult),
                r.SettlementTimestamp?.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture) ?? "",
                N(ll)));
        }
    }

    // ── summary: overall + per competition type
    var summaryPath = Path.Combine(outDir, "shadow_summary.csv");
    using (var w = new StreamWriter(summaryPath, false))
    {
        w.WriteLine("Scope,Group,DateRange,PredictionCount,Accepted,Rejected,Settled,Unsettled," +
                    "AverageHomeProbability,AverageDrawProbability,AverageAwayProbability," +
                    "LogLoss,Brier,RPS,Accuracy");

        void Emit(string scope, string group, IReadOnlyList<dynamic> set)
        {
            if (set.Count == 0) return;
            var acc = set.Where(x => (bool)x.PredictionEligible).ToList();
            var settled = acc.Where(x => x.ActualResult is not null).ToList();
            var range = $"{set.Min(x => (DateTime)x.MatchDate):yyyy-MM-dd}..{set.Max(x => (DateTime)x.MatchDate):yyyy-MM-dd}";

            double ll = 0, brier = 0, rps = 0; var correct = 0;
            foreach (var x in settled)
            {
                double ph = x.HomeProbability, pd = x.DrawProbability, pa = x.AwayProbability;
                string actual = x.ActualResult;
                var p = actual == "HomeWin" ? ph : actual == "Draw" ? pd : pa;
                ll += -Math.Log(Math.Max(p, 1e-15));
                double yh = actual == "HomeWin" ? 1 : 0, yd = actual == "Draw" ? 1 : 0, ya = actual == "AwayWin" ? 1 : 0;
                brier += (ph - yh) * (ph - yh) + (pd - yd) * (pd - yd) + (pa - ya) * (pa - ya);
                var c1 = ph - yh; var c2 = ph + pd - (yh + yd);
                rps += (c1 * c1 + c2 * c2) / 2.0;
                var argmax = ph >= pd && ph >= pa ? "HomeWin" : pd >= pa ? "Draw" : "AwayWin";
                if (argmax == actual) correct++;
            }
            var n = settled.Count;
            string F(double v) => n == 0 ? "" : v.ToString("0.00000000", CultureInfo.InvariantCulture);

            w.WriteLine(string.Join(',', scope, Q(group), range,
                set.Count.ToString(CultureInfo.InvariantCulture),
                acc.Count.ToString(CultureInfo.InvariantCulture),
                (set.Count - acc.Count).ToString(CultureInfo.InvariantCulture),
                n.ToString(CultureInfo.InvariantCulture),
                (acc.Count - n).ToString(CultureInfo.InvariantCulture),
                acc.Count == 0 ? "" : ((double)acc.Average(x => (double)x.HomeProbability)).ToString("0.00000000", CultureInfo.InvariantCulture),
                acc.Count == 0 ? "" : ((double)acc.Average(x => (double)x.DrawProbability)).ToString("0.00000000", CultureInfo.InvariantCulture),
                acc.Count == 0 ? "" : ((double)acc.Average(x => (double)x.AwayProbability)).ToString("0.00000000", CultureInfo.InvariantCulture),
                F(n == 0 ? 0 : ll / n), F(n == 0 ? 0 : brier / n), F(n == 0 ? 0 : rps / n),
                F(n == 0 ? 0 : (double)correct / n)));
        }

        var all = rows.Cast<dynamic>().ToList();
        Emit("OVERALL", "all", all);
        foreach (var g in rows.GroupBy(r =>
                 {
                     var c = CanonicalIdentityResolver.ResolveCompetition(r.LeagueId);
                     return c is null ? "OUT_OF_SCOPE" : CanonicalIdentityResolver.ResolveCompetitionType(c, r.Round);
                 }).OrderBy(g => g.Key, StringComparer.Ordinal))
            Emit("COMPETITION_TYPE", g.Key, g.Cast<dynamic>().ToList());
        foreach (var g in rows.GroupBy(r => r.ConfidenceClass).OrderBy(g => g.Key, StringComparer.Ordinal))
            Emit("CONFIDENCE_CLASS", g.Key, g.Cast<dynamic>().ToList());
    }

    Console.WriteLine();
    Console.WriteLine($"rows in Predictions        : {rows.Count}");
    Console.WriteLine($"  accepted                 : {rows.Count(r => r.PredictionEligible)}");
    Console.WriteLine($"  rejected                 : {rows.Count(r => !r.PredictionEligible)}");
    Console.WriteLine($"  settled                  : {rows.Count(r => r.ActualResult is not null)}");
    Console.WriteLine($"written: {predPath}");
    Console.WriteLine($"written: {summaryPath}");
    return 0;
}
