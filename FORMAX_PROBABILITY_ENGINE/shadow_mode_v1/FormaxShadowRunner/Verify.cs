using System.Globalization;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.ShadowRunner;

/// <summary>
/// §18 kabul kriterleri — CANLI veritabanına karşı ölçülür, varsayılmaz.
///
/// Değişmezlik ve settlement kuralları burada gerçekten DENENİR: üretim kod yolundan (EF)
/// bir UPDATE gönderilir ve reddedilmesi beklenir. "Trigger var" demek yeterli değildir;
/// "trigger denendi ve engelledi" gerekir.
/// </summary>
public static class Verify
{
    public static async Task<int> RunAsync(FormaxDbContext db, string outDir)
    {
        var results = new List<(string check, string expected, string observed, bool ok)>();
        void Check(string c, string e, string o, bool ok) => results.Add((c, e, o, ok));

        // ── 1. tablolar ve satırlar
        var total = await db.Predictions.CountAsync();
        var accepted = await db.Predictions.CountAsync(p => p.PredictionEligible);
        var rejected = total - accepted;
        Check("Predictions rows", "> 0", total.ToString(CultureInfo.InvariantCulture), total > 0);

        // ── 2. duplicate insert = 0 (benzersiz PredictionId)
        var distinctIds = await db.Predictions.Select(p => p.PredictionId).Distinct().CountAsync();
        Check("duplicate PredictionId", "0", (total - distinctIds).ToString(CultureInfo.InvariantCulture),
            total == distinctIds);

        // aynı maç için birden fazla satır varsa bunlar FARKLI PredictionId taşımalı (yeni tahmin,
        // düzeltme değil)
        var perMatch = await db.Predictions.GroupBy(p => p.MatchId)
            .Select(g => new { g.Key, Rows = g.Count(), Ids = g.Select(x => x.PredictionId).Distinct().Count() })
            .Where(x => x.Rows != x.Ids).CountAsync();
        Check("matches whose repeat predictions reused an id", "0", perMatch.ToString(CultureInfo.InvariantCulture),
            perMatch == 0);

        // ── 3. probability simplex, canlı satırlar üzerinde
        var probeRows = await db.Predictions.Where(p => p.PredictionEligible)
            .Select(p => new { p.HomeProbability, p.DrawProbability, p.AwayProbability }).ToListAsync();
        var maxSumErr = 0.0;
        var outOfRange = 0;
        foreach (var r in probeRows)
        {
            var s = (r.HomeProbability ?? 0) + (r.DrawProbability ?? 0) + (r.AwayProbability ?? 0);
            maxSumErr = Math.Max(maxSumErr, Math.Abs(s - 1.0));
            foreach (var p in new[] { r.HomeProbability, r.DrawProbability, r.AwayProbability })
                if (p is null || p < 0 || p > 1) outOfRange++;
        }
        Check("probability sum error", "<= 1e-12", maxSumErr.ToString("0.0e+0", CultureInfo.InvariantCulture),
            probeRows.Count == 0 || maxSumErr <= 1e-12);
        Check("probabilities outside [0,1]", "0", outOfRange.ToString(CultureInfo.InvariantCulture), outOfRange == 0);

        // ── 4. reddedilen satır sayı taşımaz
        var leaked = await db.Predictions.CountAsync(p => !p.PredictionEligible &&
            (p.HomeProbability != null || p.DrawProbability != null || p.AwayProbability != null));
        Check("rejected rows carrying a probability", "0", leaked.ToString(CultureInfo.InvariantCulture), leaked == 0);

        // ── 5. leakage: kanıt maçtan kesinlikle önceki bir GÜNDE
        var leak = await db.Predictions.CountAsync(p => p.EvidenceCutoff != null && p.EvidenceCutoff >= p.MatchDate);
        Check("evidence dated at or after the match", "0", leak.ToString(CultureInfo.InvariantCulture), leak == 0);

        // ── 6. versiyonlama
        var badVersion = await db.Predictions.CountAsync(p =>
            p.ModelVersion != "INDEPENDENT_POISSON_V2" || p.TeamStrengthVersion != "TEAM_STRENGTH_V2" ||
            p.GateVersion != "GATE_V1" || p.CalibrationVersion != "NONE");
        Check("rows not stamped with the locked versions", "0", badVersion.ToString(CultureInfo.InvariantCulture),
            badVersion == 0);

        // ── 7. shadow mode: hiçbir satır kullanıcıya açık değil
        var nonShadow = await db.Predictions.CountAsync(p => !p.ShadowMode);
        Check("rows not marked ShadowMode", "0", nonShadow.ToString(CultureInfo.InvariantCulture), nonShadow == 0);

        // ── 8. DEĞİŞMEZLİK — canlı deneme, üretim kod yolundan (EF)
        var victim = await db.Predictions.FirstOrDefaultAsync(p => p.PredictionEligible);
        if (victim is null)
        {
            Check("live probe: UPDATE a published prediction via EF", "REJECTED", "no row to probe", false);
        }
        else
        {
            var originalHome = victim.HomeProbability;
            var originalHash = victim.ContentHash;
            victim.ConfidenceClass = victim.ConfidenceClass == "HIGH" ? "LOW" : "HIGH";
            var blocked = false;
            string message;
            try
            {
                await db.SaveChangesAsync();
                message = "SUCCEEDED";
            }
            catch (Exception ex)
            {
                blocked = true;
                message = (ex.InnerException?.Message ?? ex.Message).Split('\n')[0].Trim();
                if (message.Length > 90) message = message[..90] + "…";
            }
            db.ChangeTracker.Clear();

            Check("live probe: UPDATE a published prediction via EF", "REJECTED",
                blocked ? "REJECTED — " + message : "SUCCEEDED (immutability broken)", blocked);

            var after = await db.Predictions.AsNoTracking()
                .FirstAsync(p => p.PredictionId == victim.PredictionId);
            Check("stored probability after the UPDATE attempt",
                originalHome?.ToString("0.00000000", CultureInfo.InvariantCulture) ?? "null",
                after.HomeProbability?.ToString("0.00000000", CultureInfo.InvariantCulture) ?? "null",
                after.HomeProbability == originalHome);
            Check("stored ContentHash after the UPDATE attempt", originalHash, after.ContentHash,
                after.ContentHash == originalHash);
        }

        // ── 9. SETTLEMENT 1:1 — canlı deneme
        var settledCount = await db.PredictionSettlements.CountAsync();
        Check("settlement rows", ">= 0", settledCount.ToString(CultureInfo.InvariantCulture), true);

        var orphan = await db.PredictionSettlements
            .CountAsync(s => !db.Predictions.Any(p => p.PredictionId == s.PredictionId));
        Check("settlements without a prediction", "0", orphan.ToString(CultureInfo.InvariantCulture), orphan == 0);

        // NOT: settlement 1:1 ve cift-settlement reddi burada DENENMEZ.
        // Onceki surumde bu prob GERCEK bir golge tahminine SAHTE bir sonuc iliştiriyor, sonra
        // siliyordu. Append-only koruma eklendikten sonra silme de engellendi ve prob gercek
        // golge kaydinda sahte bir sonuc birakti. Dogrulama, gozlemledigi veriyi kirletmemeli.
        // Ayni kurallar "eftest" komutunda KENDI sentetik satirlariyla sinaniyor.
        Check("settlement 1:1 and duplicate rejection", "covered by eftest on synthetic rows",
            "not probed here (would contaminate real shadow data)", true);

        // ── rapor
        Console.WriteLine();
        Console.WriteLine("== §18 ACCEPTANCE CRITERIA — measured against the live database ==");
        foreach (var (c, e, o, ok) in results)
            Console.WriteLine($"  {(ok ? "PASS" : "FAIL"),-5} {c}: expected {e}, observed {o}");

        Directory.CreateDirectory(outDir);
        var path = Path.Combine(outDir, "shadow_acceptance.csv");
        using (var w = new StreamWriter(path, false))
        {
            w.WriteLine("Check,Expected,Observed,Status");
            foreach (var (c, e, o, ok) in results)
                w.WriteLine($"{Csv(c)},{Csv(e)},{Csv(o)},{(ok ? "PASS" : "FAIL")}");
        }
        Console.WriteLine();
        Console.WriteLine($"accepted {accepted} / rejected {rejected} / total {total}   written: {path}");
        return results.All(r => r.ok) ? 0 : 3;
    }

    private static string Csv(string s) => s.Contains(',') || s.Contains('"')
        ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
}
