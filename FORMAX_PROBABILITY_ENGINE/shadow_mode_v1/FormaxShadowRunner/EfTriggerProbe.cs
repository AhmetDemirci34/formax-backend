using System.Globalization;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using PredictionEntity = Formax.Domain.Entities.Prediction;

namespace Formax.ShadowRunner;

/// <summary>
/// §2 — EF Core 8 + SQL Server + trigger etkileşimini ÜRETİM YOLUNDAN sınar.
///
/// Neden ayrı bir prob gerekiyor: SQL Server, tetikleyicisi olan bir tabloya `OUTPUT` yan tümceli
/// (INTO'suz) DML kabul etmez. EF Core 8 tek satırlık INSERT'te identity değerini almak için tam
/// olarak bunu üretir; ÇOK satırlı INSERT'te ise `MERGE … OUTPUT INTO @inserted` üretir ve bu
/// trigger ile uyumludur.
///
/// Gölge modun ilk koşusu 65 satırı TEK SaveChanges ile yazdı, yani toplu yolu kullandı ve
/// çalıştı. Bu, tek satırlık yolun da çalıştığı anlamına GELMEZ. Üretimde bir maç için tek tahmin
/// yazılırsa o yol devreye girer. Bu prob ikisini de ayrı ayrı dener.
/// </summary>
public static class EfTriggerProbe
{
    public static async Task<int> RunAsync(FormaxDbContext db, string outDir)
    {
        var results = new List<(string check, string expected, string observed, bool ok)>();
        void Check(string c, string e, string o, bool ok) => results.Add((c, e, o, ok));

        var stamp = DateTime.UtcNow.ToString("HHmmssfff", CultureInfo.InvariantCulture);
        var ids = new List<string>();

        PredictionEntity Make(string suffix) => new()
        {
            PredictionId = ("FMXPPROBE" + stamp + suffix).PadRight(24, '0')[..24],
            MatchId = -1,
            CanonicalMatchId = "PROBE",
            MatchDate = new DateTime(2027, 1, 10, 18, 0, 0, DateTimeKind.Utc),
            PredictionTimestamp = new DateTime(2027, 1, 8, 0, 0, 0, DateTimeKind.Utc),
            EvidenceCutoff = new DateTime(2027, 1, 5, 0, 0, 0, DateTimeKind.Utc),
            ModelVersion = "INDEPENDENT_POISSON_V2",
            TeamStrengthVersion = "TEAM_STRENGTH_V2",
            GateVersion = "GATE_V1",
            CalibrationVersion = "NONE",
            HomeProbability = 0.5,
            DrawProbability = 0.25,
            AwayProbability = 0.25,
            PredictionEligible = true,
            ConfidenceClass = "HIGH",
            GateStatus = "ACCEPTED",
            GateReason = "OK",
            ContentHash = "PROBE" + stamp,
            ShadowMode = true,
            CreatedAt = DateTime.UtcNow
        };

        // ── 1. TEK SATIRLIK EF INSERT — üretimde bir maç için tek tahmin yazılan yol
        var single = Make("S");
        ids.Add(single.PredictionId);
        var singleOk = false;
        string singleMsg;
        try
        {
            db.Predictions.Add(single);
            await db.SaveChangesAsync();
            singleOk = true;
            singleMsg = $"INSERTED (Sequence={single.Sequence})";
        }
        catch (Exception ex)
        {
            singleMsg = Short(ex);
        }
        db.ChangeTracker.Clear();
        Check("EF single-row INSERT", "ACCEPTED", singleMsg, singleOk);

        // identity geri okundu mu? (OUTPUT yolu kırılırsa Sequence 0 kalır)
        if (singleOk)
            Check("identity value returned to EF after single insert", "> 0",
                single.Sequence.ToString(CultureInfo.InvariantCulture), single.Sequence > 0);

        // ── 2. TOPLU EF INSERT — gölge job'ın kullandığı yol
        var batch = new[] { Make("B1"), Make("B2"), Make("B3") };
        foreach (var b in batch) ids.Add(b.PredictionId);
        var batchOk = false;
        string batchMsg;
        try
        {
            db.Predictions.AddRange(batch);
            await db.SaveChangesAsync();
            batchOk = true;
            batchMsg = $"INSERTED ({batch.Length} rows, Sequences {string.Join('/', batch.Select(b => b.Sequence))})";
        }
        catch (Exception ex)
        {
            batchMsg = Short(ex);
        }
        db.ChangeTracker.Clear();
        Check("EF batched INSERT", "ACCEPTED", batchMsg, batchOk);

        // ── 3. EF UPDATE — reddedilmeli, ve TRIGGER tarafından reddedilmeli
        var updateBlocked = false;
        var blockedByTrigger = false;
        string updateMsg = "no row to probe";
        if (singleOk)
        {
            var victim = await db.Predictions.FirstAsync(p => p.PredictionId == single.PredictionId);
            victim.ConfidenceClass = "LOW";
            try
            {
                await db.SaveChangesAsync();
                updateMsg = "SUCCEEDED (immutability broken)";
            }
            catch (Exception ex)
            {
                updateBlocked = true;
                updateMsg = Short(ex);
                blockedByTrigger = updateMsg.Contains("INSERT-ONLY", StringComparison.Ordinal);
            }
            db.ChangeTracker.Clear();
        }
        Check("EF UPDATE", "REJECTED", updateMsg, updateBlocked);
        Check("EF UPDATE rejected BY THE TRIGGER (not by an EF/SQL incompatibility)",
            "trigger message", blockedByTrigger ? "trigger message" : "other mechanism", blockedByTrigger);

        // ── 4. EF DELETE — reddedilmeli
        var deleteBlocked = false;
        string deleteMsg = "no row to probe";
        if (singleOk)
        {
            var victim = await db.Predictions.FirstAsync(p => p.PredictionId == single.PredictionId);
            db.Predictions.Remove(victim);
            try
            {
                await db.SaveChangesAsync();
                deleteMsg = "SUCCEEDED (append-only broken)";
            }
            catch (Exception ex)
            {
                deleteBlocked = true;
                deleteMsg = Short(ex);
            }
            db.ChangeTracker.Clear();
        }
        Check("EF DELETE", "REJECTED", deleteMsg, deleteBlocked);

        // ── 5. settlement INSERT + çift settlement reddi (EF yolundan)
        if (singleOk)
        {
            var settleOk = false;
            string settleMsg;
            try
            {
                db.PredictionSettlements.Add(new PredictionSettlement
                {
                    PredictionId = single.PredictionId,
                    ActualHomeGoals = 2, ActualAwayGoals = 1, ActualResult = "HomeWin",
                    SettlementTimestamp = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
                settleOk = true;
                settleMsg = "INSERTED";
            }
            catch (Exception ex) { settleMsg = Short(ex); }
            db.ChangeTracker.Clear();
            Check("EF settlement INSERT", "ACCEPTED", settleMsg, settleOk);

            if (settleOk)
            {
                var dupBlocked = false;
                string dupMsg;
                try
                {
                    db.PredictionSettlements.Add(new PredictionSettlement
                    {
                        PredictionId = single.PredictionId,
                        ActualHomeGoals = 0, ActualAwayGoals = 3, ActualResult = "AwayWin",
                        SettlementTimestamp = DateTime.UtcNow
                    });
                    await db.SaveChangesAsync();
                    dupMsg = "SUCCEEDED (1:1 broken)";
                }
                catch (Exception ex) { dupBlocked = true; dupMsg = Short(ex); }
                db.ChangeTracker.Clear();
                Check("EF duplicate settlement", "REJECTED", dupMsg, dupBlocked);

                // settlement tahmini değiştirdi mi?
                var after = await db.Predictions.AsNoTracking().FirstAsync(p => p.PredictionId == single.PredictionId);
                Check("prediction probability after settlement", "0.50000000",
                    after.HomeProbability?.ToString("0.00000000", CultureInfo.InvariantCulture) ?? "null",
                    after.HomeProbability == 0.5);
                Check("prediction ConfidenceClass after settlement", "HIGH", after.ConfidenceClass,
                    after.ConfidenceClass == "HIGH");
            }
        }

        // ── temizlik: prob satırları gerçek gölge verisi değil. Trigger'lar DELETE'i engellediği
        // için geçici olarak devre dışı bırakılır - bu, kasıtlı ve görünür bir eylemdir.
        try
        {
            await db.Database.ExecuteSqlRawAsync("DISABLE TRIGGER dbo.TR_PredictionSettlements_NoDelete ON dbo.PredictionSettlements;");
            await db.Database.ExecuteSqlRawAsync("DISABLE TRIGGER dbo.TR_Predictions_NoDelete ON dbo.Predictions;");
            foreach (var id in ids)
            {
                await db.Database.ExecuteSqlRawAsync("DELETE FROM dbo.PredictionSettlements WHERE PredictionId = {0}", id);
                await db.Database.ExecuteSqlRawAsync("DELETE FROM dbo.Predictions WHERE PredictionId = {0}", id);
            }
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("ENABLE TRIGGER dbo.TR_Predictions_NoDelete ON dbo.Predictions;");
            await db.Database.ExecuteSqlRawAsync("ENABLE TRIGGER dbo.TR_PredictionSettlements_NoDelete ON dbo.PredictionSettlements;");
        }

        var leftovers = await db.Predictions.CountAsync(p => p.MatchId == -1);
        Check("probe rows cleaned up", "0", leftovers.ToString(CultureInfo.InvariantCulture), leftovers == 0);

        var triggersEnabled = await db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM sys.triggers WHERE parent_id IN (OBJECT_ID('dbo.Predictions'),OBJECT_ID('dbo.PredictionSettlements')) AND is_disabled = 0")
            .FirstAsync();
        Check("all four triggers re-enabled after cleanup", "4",
            triggersEnabled.ToString(CultureInfo.InvariantCulture), triggersEnabled == 4);

        Console.WriteLine();
        Console.WriteLine("== §2/§3 EF + TRIGGER PROBE — production path, live database ==");
        foreach (var (c, e, o, ok) in results)
            Console.WriteLine($"  {(ok ? "PASS" : "FAIL"),-5} {c}: expected {e}, observed {o}");

        Directory.CreateDirectory(outDir);
        var path = Path.Combine(outDir, "ef_trigger_probe.csv");
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

    private static string Short(Exception ex)
    {
        var m = (ex.InnerException?.Message ?? ex.Message).Split('\n')[0].Trim();
        return m.Length > 110 ? m[..110] + "…" : m;
    }

    private static string Csv(string s) => s.Contains(',') || s.Contains('"')
        ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
}
