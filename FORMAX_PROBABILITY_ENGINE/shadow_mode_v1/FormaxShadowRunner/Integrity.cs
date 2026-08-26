using System.Globalization;
using Formax.Contract.Models;
using Formax.Contract.Services;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Predictions;
using Microsoft.EntityFrameworkCore;

namespace Formax.ShadowRunner;

/// <summary>
/// §10 — veri bütünlüğü, CANLI tablo üzerinde.
///
/// En önemli kontrol <see cref="PredictionContract.ContentHash"/> doğrulamasıdır: satırdaki
/// alanlardan hash YENİDEN hesaplanır ve saklananla karşılaştırılır. Bu, "satır yayımlandığı gibi
/// mi?" sorusunun tek gerçek cevabıdır — trigger'lar UPDATE'i engelliyor olsa bile, hash'i
/// bağımsız olarak yeniden hesaplamadan satırın bozulmadığını bilemeyiz (yanlış yazılmış bir
/// INSERT'i hiçbir trigger yakalamaz).
/// </summary>
public static class Integrity
{
    public static async Task<int> RunAsync(FormaxDbContext db, string outDir)
    {
        var results = new List<(string check, string expected, string observed, bool ok)>();
        void Check(string c, string e, string o, bool ok) => results.Add((c, e, o, ok));

        var rows = await db.Predictions.AsNoTracking().OrderBy(p => p.Sequence).ToListAsync();
        Check("prediction rows", "> 0", rows.Count.ToString(CultureInfo.InvariantCulture), rows.Count > 0);

        // ── PredictionId benzersiz
        var distinctIds = rows.Select(r => r.PredictionId).Distinct().Count();
        Check("PredictionId unique", rows.Count.ToString(CultureInfo.InvariantCulture),
            distinctIds.ToString(CultureInfo.InvariantCulture), distinctIds == rows.Count);

        // ── Sequence: kesintisiz artan ve benzersiz
        var seqDistinct = rows.Select(r => r.Sequence).Distinct().Count();
        var monotonic = true;
        for (var i = 1; i < rows.Count; i++) if (rows[i].Sequence <= rows[i - 1].Sequence) monotonic = false;
        Check("Sequence unique", rows.Count.ToString(CultureInfo.InvariantCulture),
            seqDistinct.ToString(CultureInfo.InvariantCulture), seqDistinct == rows.Count);
        Check("Sequence strictly increasing in publication order", "yes", monotonic ? "yes" : "no", monotonic);

        // ── ContentHash: satırdan YENİDEN hesapla
        var hashMismatch = 0;
        string? firstMismatch = null;
        foreach (var r in rows)
        {
            var rebuilt = new PredictionContract
            {
                PredictionId = r.PredictionId,
                MatchId = r.CanonicalMatchId ?? "",
                MatchDate = DateOnly.FromDateTime(r.MatchDate),
                PredictionTimestamp = DateTime.SpecifyKind(r.PredictionTimestamp, DateTimeKind.Utc),
                EvidenceCutoff = r.EvidenceCutoff.HasValue ? DateOnly.FromDateTime(r.EvidenceCutoff.Value) : null,
                Versions = new ContractVersions(r.ModelVersion, r.TeamStrengthVersion, r.GateVersion, r.CalibrationVersion),
                HomeProbability = r.HomeProbability,
                DrawProbability = r.DrawProbability,
                AwayProbability = r.AwayProbability,
                PredictionEligible = r.PredictionEligible,
                ConfidenceClass = Enum.Parse<Formax.Contract.Models.ConfidenceClass>(r.ConfidenceClass, ignoreCase: true),
                GateStatus = Enum.Parse<GateStatus>(r.GateStatus, ignoreCase: true),
                GateReason = r.GateReason
            };
            if (rebuilt.ContentHash != r.ContentHash)
            {
                hashMismatch++;
                firstMismatch ??= $"{r.PredictionId}: stored {r.ContentHash}, recomputed {rebuilt.ContentHash}";
            }
        }
        Check("ContentHash recomputed from the stored row matches what was published", "0 mismatches",
            hashMismatch == 0 ? "0 mismatches" : $"{hashMismatch} — {firstMismatch}", hashMismatch == 0);

        // ── PredictionId de yeniden türetilebilmeli (kimliğin deterministik olduğunun kanıtı)
        var idMismatch = 0;
        foreach (var r in rows)
        {
            var expected = PredictionIdFactory.Create(
                r.CanonicalMatchId ?? "",
                new ContractVersions(r.ModelVersion, r.TeamStrengthVersion, r.GateVersion, r.CalibrationVersion),
                r.EvidenceCutoff.HasValue ? DateOnly.FromDateTime(r.EvidenceCutoff.Value) : null,
                DateTime.SpecifyKind(r.PredictionTimestamp, DateTimeKind.Utc));
            if (expected != r.PredictionId) idMismatch++;
        }
        Check("PredictionId recomputed from the stored row matches", "0 mismatches",
            idMismatch.ToString(CultureInfo.InvariantCulture), idMismatch == 0);

        // ── orphan settlement / orphan prediction
        var orphanSettlement = await db.PredictionSettlements
            .CountAsync(s => !db.Predictions.Any(p => p.PredictionId == s.PredictionId));
        Check("orphan settlements", "0", orphanSettlement.ToString(CultureInfo.InvariantCulture), orphanSettlement == 0);

        var orphanPrediction = await db.Predictions
            .CountAsync(p => !db.Matches.Any(m => m.Id == p.MatchId));
        Check("predictions pointing at a match that does not exist", "0",
            orphanPrediction.ToString(CultureInfo.InvariantCulture), orphanPrediction == 0);

        // ── duplicate prediction: aynı maç + aynı kanıt kesimi iki kez yazılmamalı
        var dupSameEvidence = rows.GroupBy(r => (r.MatchId, r.EvidenceCutoff))
            .Count(g => g.Count() > 1);
        Check("same match written twice with the same evidence cutoff", "0",
            dupSameEvidence.ToString(CultureInfo.InvariantCulture), dupSameEvidence == 0);

        // aynı maçın birden fazla satırı VARSA bu farklı kanıt kesimi demektir (yeni tahmin)
        var multi = rows.GroupBy(r => r.MatchId).Where(g => g.Count() > 1).ToList();
        var allDifferentEvidence = multi.All(g => g.Select(x => x.EvidenceCutoff).Distinct().Count() == g.Count());
        Check("matches with several predictions differ in evidence cutoff",
            $"{multi.Count} matches, all differ",
            multi.Count == 0 ? "no repeats" : (allDifferentEvidence ? $"{multi.Count} matches, all differ" : "some share a cutoff"),
            allDifferentEvidence);

        // ── §6 EvidenceCutoff: tek ihlal FAIL
        var leak = rows.Count(r => r.EvidenceCutoff.HasValue && r.EvidenceCutoff.Value.Date >= r.MatchDate.Date);
        Check("EvidenceCutoff violations (evidence on or after the match day)", "0",
            leak.ToString(CultureInfo.InvariantCulture), leak == 0);

        // ── §4 simplex ve versiyon damgası
        var simplexErr = 0.0;
        var badVersion = 0;
        foreach (var r in rows)
        {
            if (r.PredictionEligible)
                simplexErr = Math.Max(simplexErr,
                    Math.Abs((r.HomeProbability ?? 0) + (r.DrawProbability ?? 0) + (r.AwayProbability ?? 0) - 1.0));
            if (r.ModelVersion != "INDEPENDENT_POISSON_V2" || r.TeamStrengthVersion != "TEAM_STRENGTH_V2"
                || r.GateVersion != "GATE_V1" || r.CalibrationVersion != "NONE") badVersion++;
        }
        Check("probability sum error", "<= 1e-12", simplexErr.ToString("0.0e+0", CultureInfo.InvariantCulture),
            simplexErr <= 1e-12);
        Check("rows not stamped with the locked versions", "0",
            badVersion.ToString(CultureInfo.InvariantCulture), badVersion == 0);

        Console.WriteLine();
        Console.WriteLine("== §10 DATA INTEGRITY — live table, values recomputed not trusted ==");
        foreach (var (c, e, o, ok) in results)
            Console.WriteLine($"  {(ok ? "PASS" : "FAIL"),-5} {c}: expected {e}, observed {o}");

        Directory.CreateDirectory(outDir);
        var path = Path.Combine(outDir, "data_integrity.csv");
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
}
