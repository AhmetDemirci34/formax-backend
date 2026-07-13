using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Historical.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.Historical.Dataset;

/// <summary>Dataset v1 üretimi sözleşmesi. Deterministik + idempotent (aynı Feature Store → aynı dataset).</summary>
public interface IDatasetBuilder
{
    Task<Formax.Infrastructure.Historical.Dataset.Dataset> BuildAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Probability Engine için Dataset v1'i üretir. TEK veri kaynağı = Feature Store (Historical'a dokunmaz;
/// feature'ları yeniden hesaplamaz). Deterministik stratified split (train/val/test = 70/15/15): sınıf
/// dağılımı korunur, atama yalnız MatchId'nin stabil hash'ine bağlıdır → tekrar çalıştırınca AYNI split.
/// Her maç tek split'e girer (duplicate/leakage yok). Target label feature DEĞİLdir (X'te yer almaz).
/// </summary>
public sealed class DatasetBuilder : IDatasetBuilder
{
    private const double TrainRatio = 0.70, ValRatio = 0.15;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly FormaxDbContext _db;
    private readonly ILogger<DatasetBuilder> _logger;

    public DatasetBuilder(FormaxDbContext db, ILogger<DatasetBuilder> logger)
    {
        _db = db;
        _logger = logger;
    }

    private readonly record struct Row(int MatchId, double[] Features, int Label);

    public async Task<Formax.Infrastructure.Historical.Dataset.Dataset> BuildAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        var total = await _db.MatchFeatureRecords.AsNoTracking().CountAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Dataset v1: Feature Store'dan {N} kayıt okunuyor...", total);

        var rows = new List<Row>(total);
        var excludedNoTarget = 0;
        var processed = 0;

        // Feature Store TEK kaynak: features + target buradan. Historical'a gidilmez.
        await foreach (var rec in _db.MatchFeatureRecords.AsNoTracking()
            .OrderBy(x => x.HistoricalMatchId)
            .Select(x => new { x.HistoricalMatchId, x.FeaturesJson, x.TargetResult })
            .AsAsyncEnumerable().WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            processed++;
            if (rec.TargetResult is not ("H" or "D" or "A")) { excludedNoTarget++; continue; }

            var vector = JsonSerializer.Deserialize<MatchFeatureVector>(rec.FeaturesJson, Json);
            if (vector is null) { excludedNoTarget++; continue; }

            var label = rec.TargetResult switch { "H" => 0, "D" => 1, _ => 2 };
            rows.Add(new Row(rec.HistoricalMatchId, FeatureFlattener.Flatten(vector), label));

            if (processed % 50000 == 0)
                _logger.LogInformation("Dataset v1 ilerleme: {P}/{T} okundu ({S} örnek)", processed, total, rows.Count);
        }

        // Deterministik STRATIFIED split: her sınıf içinde stabil hash sırasına göre 70/15/15.
        var train = new List<DatasetSample>();
        var val = new List<DatasetSample>();
        var test = new List<DatasetSample>();

        foreach (var group in rows.GroupBy(r => r.Label))
        {
            var ordered = group.OrderBy(r => SplitKey(r.MatchId)).ThenBy(r => r.MatchId).ToList();
            var n = ordered.Count;
            var trainEnd = (int)(n * TrainRatio);
            var valEnd = trainEnd + (int)(n * ValRatio);

            for (var i = 0; i < n; i++)
            {
                var split = i < trainEnd ? "train" : i < valEnd ? "validation" : "test";
                var target = split == "train" ? train : split == "validation" ? val : test;
                target.Add(new DatasetSample { MatchId = ordered[i].MatchId, Features = ordered[i].Features, Label = ordered[i].Label, Split = split });
            }
        }

        sw.Stop();
        var peak = Process.GetCurrentProcess().PeakWorkingSet64 / (1024 * 1024);
        var samplesTotal = train.Count + val.Count + test.Count;

        var metadata = new DatasetMetadata
        {
            Version = "v1",
            BuiltAtUtc = DateTime.UtcNow,
            FeatureCount = FeatureFlattener.Count,
            FeatureNames = FeatureFlattener.FeatureNames,
            TotalSamples = samplesTotal,
            ExcludedNoTarget = excludedNoTarget,
            TrainCount = train.Count,
            ValidationCount = val.Count,
            TestCount = test.Count,
            TrainDistribution = Distribution(train),
            ValidationDistribution = Distribution(val),
            TestDistribution = Distribution(test),
            TrainRatio = samplesTotal == 0 ? 0 : train.Count / (double)samplesTotal,
            ValidationRatio = samplesTotal == 0 ? 0 : val.Count / (double)samplesTotal,
            TestRatio = samplesTotal == 0 ? 0 : test.Count / (double)samplesTotal,
            ElapsedMs = sw.ElapsedMilliseconds,
            PeakMemoryMB = peak
        };

        _logger.LogInformation("Dataset v1 BİTTİ: {Tot} örnek (train {Tr}, val {Va}, test {Te}), {F} feature, {Ms} ms",
            samplesTotal, train.Count, val.Count, test.Count, metadata.FeatureCount, metadata.ElapsedMs);

        return new Formax.Infrastructure.Historical.Dataset.Dataset { Train = train, Validation = val, Test = test, Metadata = metadata };
    }

    private static SplitClassDistribution Distribution(List<DatasetSample> s) => new()
    {
        Home = s.Count(x => x.Label == 0),
        Draw = s.Count(x => x.Label == 1),
        Away = s.Count(x => x.Label == 2)
    };

    /// <summary>MatchId'nin stabil (runtime-bağımsız) hash'i — deterministik split sırası için.</summary>
    private static long SplitKey(int matchId)
        => (long)unchecked((ulong)matchId * 0x9E3779B97F4A7C15UL) & long.MaxValue;
}
