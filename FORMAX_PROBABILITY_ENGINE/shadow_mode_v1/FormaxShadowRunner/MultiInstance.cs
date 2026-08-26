using System.Globalization;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Predictions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Formax.ShadowRunner;

/// <summary>
/// §11 — çok örnekli (scale-out) davranış, GERÇEK bir yarış üretilerek ölçülür.
///
/// Neden özel bir kurulum gerekiyor: iki örnek aynı ufukla koşarsa ikisi de aynı 65 tahmini zaten
/// yazılmış bulur ve hiçbir şey yazmaz — yarış hiç oluşmaz, test hiçbir şey kanıtlamaz. Gerçek
/// yarış için iki örneğin de "bu tahmin henüz yok" görmesi ve AYNI ANDA yazmaya çalışması gerekir.
///
/// Kurulum: ufuk genişletilir, böylece henüz tahmini olmayan gerçek maçlar belirir. İki servis
/// örneği (ayrı DbContext, ayrı scope) eşzamanlı koşar. Her ikisi de cycle başında mevcut
/// kimlikleri okur, aynı deterministik PredictionId'leri hesaplar ve yazmaya çalışır.
///
/// Beklenen: benzersiz PredictionId kısıtı ikinci yazımı reddeder; satır KAYBOLMAZ ve
/// ÇOĞALMAZ. Beklenmeyen: aynı tahminin iki satırı, ya da hiç yazılmaması.
/// </summary>
public static class MultiInstance
{
    public static async Task<int> RunAsync(string engineRoot, string conn, string outDir, int horizonDays)
    {
        var results = new List<(string check, string expected, string observed, bool ok)>();
        void Check(string c, string e, string o, bool ok) => results.Add((c, e, o, ok));

        ServiceProvider BuildProvider()
        {
            var s = new ServiceCollection();
            s.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
            s.AddDbContext<FormaxDbContext>(o => o.UseSqlServer(conn));
            s.AddSingleton(new PredictionEngineOptions { EngineRoot = engineRoot });
            s.AddScoped<ShadowPredictionService>();
            return s.BuildServiceProvider();
        }

        // ── başlangıç durumu
        int Before;
        using (var p0 = BuildProvider())
        using (var sc0 = p0.CreateScope())
            Before = await sc0.ServiceProvider.GetRequiredService<FormaxDbContext>().Predictions.CountAsync();

        Console.WriteLine($"rows before: {Before}   horizon: {horizonDays} days");
        Console.WriteLine("starting two concurrent shadow instances against the same database…");

        // ── iki örnek, gerçekten eşzamanlı
        var providerA = BuildProvider();
        var providerB = BuildProvider();

        async Task<ShadowCycleReport> RunOne(ServiceProvider p)
        {
            using var scope = p.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<ShadowPredictionService>();
            return await svc.RunCycleAsync(horizonDays);
        }

        ShadowCycleReport? a = null, b = null;
        Exception? exA = null, exB = null;

        var taskA = Task.Run(async () => { try { a = await RunOne(providerA); } catch (Exception ex) { exA = ex; } });
        var taskB = Task.Run(async () => { try { b = await RunOne(providerB); } catch (Exception ex) { exB = ex; } });
        await Task.WhenAll(taskA, taskB);

        await providerA.DisposeAsync();
        await providerB.DisposeAsync();

        Console.WriteLine($"  instance A: {(exA is null ? Describe(a!) : "THREW: " + Short(exA))}");
        Console.WriteLine($"  instance B: {(exB is null ? Describe(b!) : "THREW: " + Short(exB))}");

        // ── sonuç durumu
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FormaxDbContext>();

        var after = await db.Predictions.CountAsync();
        var distinctIds = await db.Predictions.Select(p => p.PredictionId).Distinct().CountAsync();
        var inserted = after - Before;

        Console.WriteLine($"rows after: {after}  (+{inserted})");

        // Bir örneğin çökmesi kabul edilebilir DEĞİL: çakışma veri kaybı olmadan yönetilmeli.
        // Ama tek bir SaveChanges'in benzersizlik ihlaliyle reddedilmesi BEKLENEN davranıştır ve
        // servis bunu yakalayıp raporlar (ImmutabilityRefused).
        var neitherCrashed = exA is null && exB is null;
        Check("neither instance threw an unhandled exception", "0 crashes",
            neitherCrashed ? "0 crashes" : $"A:{(exA is null ? "ok" : "threw")} B:{(exB is null ? "ok" : "threw")}",
            neitherCrashed);

        Check("PredictionId still unique after concurrent writes",
            after.ToString(CultureInfo.InvariantCulture), distinctIds.ToString(CultureInfo.InvariantCulture),
            after == distinctIds);

        var dupSameEvidence = await db.Predictions
            .GroupBy(p => new { p.MatchId, p.EvidenceCutoff })
            .CountAsync(g => g.Count() > 1);
        Check("same match+evidence written twice", "0", dupSameEvidence.ToString(CultureInfo.InvariantCulture),
            dupSameEvidence == 0);

        // İki örnek de aynı maçları gördüyse, yazılan satır sayısı TEK bir örneğin yazacağı kadar
        // olmalı - iki katı değil.
        var predictedA = a?.Predicted ?? 0;
        var predictedB = b?.Predicted ?? 0;
        var expectedMax = Math.Max(a?.Inserted ?? 0, b?.Inserted ?? 0);
        Check("rows added equal one instance's work, not both",
            $"<= {Math.Max(predictedA, predictedB)}", inserted.ToString(CultureInfo.InvariantCulture),
            inserted <= Math.Max(predictedA, predictedB));

        var refused = (a?.ImmutabilityRefused ?? 0) + (b?.ImmutabilityRefused ?? 0);
        var oneWroteNothing = (a?.Inserted ?? 0) == 0 || (b?.Inserted ?? 0) == 0;
        Check("collision handled without data loss (one writer wins, or the loser is refused)",
            "handled", refused > 0 || oneWroteNothing || inserted == 0 ? "handled" : "both wrote",
            refused > 0 || oneWroteNothing || inserted == 0);

        var status = neitherCrashed && after == distinctIds && dupSameEvidence == 0
            ? "NOT_SUPPORTED_BUT_SAFE"
            : "NOT_SUPPORTED";

        Console.WriteLine();
        Console.WriteLine("== §11 MULTI-INSTANCE — two concurrent shadow instances, same database ==");
        foreach (var (c, e, o, ok) in results)
            Console.WriteLine($"  {(ok ? "PASS" : "FAIL"),-5} {c}: expected {e}, observed {o}");
        Console.WriteLine();
        Console.WriteLine($"  MULTI_INSTANCE_STATUS = {status}");
        Console.WriteLine("  (no distributed lock exists; correctness rests on the unique PredictionId constraint,");
        Console.WriteLine("   so concurrent instances duplicate WORK but cannot duplicate DATA)");

        Directory.CreateDirectory(outDir);
        var path = Path.Combine(outDir, "multi_instance.csv");
        using (var w = new StreamWriter(path, false))
        {
            w.WriteLine("Check,Expected,Observed,Status");
            foreach (var (c, e, o, ok) in results)
                w.WriteLine($"{Csv(c)},{Csv(e)},{Csv(o)},{(ok ? "PASS" : "FAIL")}");
            w.WriteLine($"MULTI_INSTANCE_STATUS,{status},{status},INFO");
        }
        Console.WriteLine();
        Console.WriteLine($"written: {path}");
        return results.All(r => r.ok) ? 0 : 3;
    }

    private static string Describe(ShadowCycleReport r) =>
        $"seen {r.UpcomingMatchesSeen}, predicted {r.Predicted}, inserted {r.Inserted}, " +
        $"already published {r.AlreadyPublished}, refused {r.ImmutabilityRefused}, failed {r.Failed}, {r.ElapsedMs} ms";

    private static string Short(Exception ex)
    {
        var m = (ex.InnerException?.Message ?? ex.Message).Split('\n')[0].Trim();
        return m.Length > 120 ? m[..120] + "…" : m;
    }

    private static string Csv(string s) => s.Contains(',') || s.Contains('"')
        ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
}
