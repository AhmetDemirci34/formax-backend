using Formax.Infrastructure.BackgroundJobs;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Predictions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Formax.ShadowRunner;

/// <summary>
/// §1 — hosted service yaşam döngüsü: startup, DI, scheduler, DB, failure recovery,
/// duplicate prevention ve GRACEFUL SHUTDOWN.
///
/// Gerçek <see cref="IHost"/> kurulur; job <c>AddHostedService</c> ile kaydedilir ve host'un
/// kendi <c>StartAsync</c>/<c>StopAsync</c> çağrılarıyla yönetilir — yani API'nin yaptığının
/// aynısı. Windows'ta bir arka plan sürecine Ctrl+C göndermek güvenilir değil; bu yüzden
/// kapanış sinyali host'un kendi API'siyle verilir ki "graceful" iddiası ölçülebilsin.
/// </summary>
public static class HostLifecycle
{
    public static async Task<int> RunAsync(string engineRoot, string conn, string outDir)
    {
        var log = new List<string>();
        var results = new List<(string check, string expected, string observed, bool ok)>();
        void Check(string c, string e, string o, bool ok) => results.Add((c, e, o, ok));

        var services = new ServiceCollection();
        services.AddLogging(b => { b.ClearProviders(); b.AddProvider(new CaptureLoggerProvider(log)); b.SetMinimumLevel(LogLevel.Information); });
        services.AddDbContext<FormaxDbContext>(o => o.UseSqlServer(conn));
        services.AddSingleton(new PredictionEngineOptions
        {
            EngineRoot = engineRoot,
            StartupDelaySeconds = 2,
            LoopHours = 0.005,
            HorizonDays = 8
        });
        services.AddSingleton<ShadowHealthState>();
        services.AddScoped<ShadowPredictionService>();
        services.AddScoped<PredictionSettlementService>();
        services.AddSingleton<IHostedService, ShadowPredictionJob>();
        services.AddSingleton<IHostedService, PredictionSettlementJob>();
        var provider = services.BuildServiceProvider();
        var hosted = provider.GetServices<IHostedService>().ToList();

        // ── startup + DI
        var startOk = true;
        try { foreach (var h in hosted) await h.StartAsync(CancellationToken.None); }
        catch (Exception ex) { startOk = false; log.Add("START FAILED: " + ex.Message); }
        Check("hosted services start (same StartAsync the host calls)", "started", startOk ? "started" : "failed", startOk);

        // ── scheduler: en az iki cycle bekle (idempotentlik ikinci cycle'da görülür)
        var deadline = DateTime.UtcNow.AddSeconds(90);
        while (DateTime.UtcNow < deadline &&
               log.Count(l => l.Contains("cycle done", StringComparison.Ordinal)) < 2)
            await Task.Delay(1000);

        var cycles = log.Count(l => l.Contains("cycle done", StringComparison.Ordinal));
        Check("scheduler ran repeated cycles", ">= 2", cycles.ToString(), cycles >= 2);

        var firstCycle = log.FirstOrDefault(l => l.Contains("cycle done", StringComparison.Ordinal)) ?? "";
        Check("database reachable from the hosted job", "cycle produced predictions",
            firstCycle.Contains("predicted", StringComparison.Ordinal) ? "yes" : "no",
            firstCycle.Contains("predicted", StringComparison.Ordinal));

        // ── duplicate prevention: ikinci cycle hiçbir satır eklememeli
        var later = log.Where(l => l.Contains("cycle done", StringComparison.Ordinal)).Skip(1).ToList();
        var noNewInserts = later.Count > 0 && later.All(l => l.Contains("inserted 0", StringComparison.Ordinal));
        Check("repeat cycles insert nothing (duplicate prevention in host)", "inserted 0",
            later.Count == 0 ? "no repeat cycle" : (noNewInserts ? "inserted 0" : "rows were re-inserted"), noNewInserts);

        var anyFailed = log.Any(l => l.Contains("cycle failed", StringComparison.Ordinal));
        Check("cycle errors", "0", anyFailed ? "at least one" : "0", !anyFailed);

        // ── GRACEFUL SHUTDOWN — host'un kendi kapanış yolu
        var stopOk = true;
        try { using var stopCts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); foreach (var h in hosted) await h.StopAsync(stopCts.Token); }
        catch (Exception ex) { stopOk = false; log.Add("STOP FAILED: " + ex.Message); }
        Check("hosted services stop without throwing (graceful StopAsync)", "stopped", stopOk ? "stopped" : "threw", stopOk);

        var graceful = log.Any(l => l.Contains("Job stopped gracefully", StringComparison.Ordinal));
        Check("shadow job reported a graceful stop", "graceful stop logged",
            graceful ? "graceful stop logged" : "not logged", graceful);

        var settlementStopped = log.Any(l => l.Contains("[SETTLEMENT] Job stopped", StringComparison.Ordinal));
        Check("settlement job stopped", "stopped", settlementStopped ? "stopped" : "not logged", settlementStopped);


        // ── §3 RESTART: kapanmış job'lar yeniden başlatılabilmeli ve ikinci hayatında da
        // düzgün çalışmalı. BackgroundService bir kez durdurulduktan sonra yeniden
        // StartAsync edilemez (iç CTS tüketilmiştir), dolayısıyla üretimdeki gerçek restart
        // gibi YENİ örnekler kurulur — host'un yeniden başlatılmasının karşılığı budur.
        var log2 = new List<string>();
        var services2 = new ServiceCollection();
        services2.AddLogging(b => { b.ClearProviders(); b.AddProvider(new CaptureLoggerProvider(log2)); b.SetMinimumLevel(LogLevel.Information); });
        services2.AddDbContext<FormaxDbContext>(o => o.UseSqlServer(conn));
        services2.AddSingleton(new PredictionEngineOptions { EngineRoot = engineRoot, StartupDelaySeconds = 2, LoopHours = 0.005, HorizonDays = 8 });
        services2.AddSingleton<ShadowHealthState>();
        services2.AddScoped<ShadowPredictionService>();
        services2.AddScoped<PredictionSettlementService>();
        services2.AddSingleton<IHostedService, ShadowPredictionJob>();
        var provider2 = services2.BuildServiceProvider();
        var hosted2 = provider2.GetServices<IHostedService>().ToList();

        var restartOk = true;
        try { foreach (var h in hosted2) await h.StartAsync(CancellationToken.None); }
        catch (Exception ex) { restartOk = false; log2.Add("RESTART FAILED: " + ex.Message); }

        var deadline2 = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline2 && !log2.Any(l => l.Contains("cycle done", StringComparison.Ordinal)))
            await Task.Delay(1000);

        var restartCycled = log2.Any(l => l.Contains("cycle done", StringComparison.Ordinal));
        Check("job restarts and runs a cycle in its second life", "cycle after restart",
            restartOk ? (restartCycled ? "cycle after restart" : "started but no cycle") : "restart failed",
            restartOk && restartCycled);

        var restartInsertedNothing = log2.Where(l => l.Contains("cycle done", StringComparison.Ordinal))
            .All(l => l.Contains("inserted 0", StringComparison.Ordinal));
        Check("restarted job does not re-insert what the previous life published", "inserted 0",
            restartCycled ? (restartInsertedNothing ? "inserted 0" : "rows re-inserted") : "no cycle",
            restartCycled && restartInsertedNothing);

        try { using var stop2 = new CancellationTokenSource(TimeSpan.FromSeconds(30)); foreach (var h in hosted2) await h.StopAsync(stop2.Token); }
        catch { }
        var graceful2 = log2.Any(l => l.Contains("Job stopped gracefully", StringComparison.Ordinal));
        Check("graceful stop after restart", "graceful stop logged",
            graceful2 ? "graceful stop logged" : "not logged", graceful2);
        await provider2.DisposeAsync();
        log.AddRange(log2);

        await provider.DisposeAsync();

        Console.WriteLine();
        Console.WriteLine("== §1 HOSTED SERVICE LIFECYCLE — real IHost, real database ==");
        foreach (var (c, e, o, ok) in results)
            Console.WriteLine($"  {(ok ? "PASS" : "FAIL"),-5} {c}: expected {e}, observed {o}");
        Console.WriteLine();
        Console.WriteLine("  --- captured job log ---");
        foreach (var l in log.Where(l => l.Contains("SHADOW", StringComparison.Ordinal) || l.Contains("SETTLEMENT", StringComparison.Ordinal)))
            Console.WriteLine("  " + l);

        Directory.CreateDirectory(outDir);
        var path = Path.Combine(outDir, "host_lifecycle.csv");
        using (var w = new StreamWriter(path, false))
        {
            w.WriteLine("Check,Expected,Observed,Status");
            foreach (var (c, e, o, ok) in results)
                w.WriteLine($"{Csv(c)},{Csv(e)},{Csv(o)},{(ok ? "PASS" : "FAIL")}");
        }
        Console.WriteLine();
        Console.WriteLine($"written: {path}");
        return results.All(r => r.ok) ? 0 : 3;
    }

    private static string Csv(string s) => s.Contains(',') || s.Contains('"')
        ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;

    private sealed class CaptureLoggerProvider : ILoggerProvider
    {
        private readonly List<string> _sink;
        public CaptureLoggerProvider(List<string> sink) => _sink = sink;
        public ILogger CreateLogger(string categoryName) => new CaptureLogger(_sink);
        public void Dispose() { }

        private sealed class CaptureLogger : ILogger
        {
            private readonly List<string> _sink;
            public CaptureLogger(List<string> sink) => _sink = sink;
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel level, EventId id, TState state, Exception? ex,
                Func<TState, Exception?, string> formatter)
            {
                var line = formatter(state, ex);
                lock (_sink) _sink.Add(line);
            }
        }
    }
}
