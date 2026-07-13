using System;
using System.Collections.Generic;

namespace Formax.Infrastructure.Historical.Dataset;

/// <summary>Tek eğitim örneği: düzleştirilmiş feature dizisi + label. Probability Engine doğrudan X/y olarak kullanır.</summary>
public sealed record DatasetSample
{
    public required int MatchId { get; init; }
    /// <summary>Feature vektörü (DatasetMetadata.FeatureNames ile aynı sırada). Null'lar 0.0 impute.</summary>
    public required double[] Features { get; init; }
    /// <summary>1X2 label: 0=Home, 1=Draw, 2=Away.</summary>
    public required int Label { get; init; }
    public required string Split { get; init; }
}

/// <summary>Bir split'in sınıf dağılımı (stratification kanıtı).</summary>
public sealed record SplitClassDistribution
{
    public int Home { get; init; }
    public int Draw { get; init; }
    public int Away { get; init; }
    public int Total => Home + Draw + Away;
}

public sealed record DatasetMetadata
{
    public string Version { get; init; } = "v1";
    public DateTime BuiltAtUtc { get; init; }
    public int FeatureCount { get; init; }
    public IReadOnlyList<string> FeatureNames { get; init; } = Array.Empty<string>();

    public int TotalSamples { get; init; }
    public int ExcludedNoTarget { get; init; }

    public int TrainCount { get; init; }
    public int ValidationCount { get; init; }
    public int TestCount { get; init; }

    public SplitClassDistribution TrainDistribution { get; init; } = new();
    public SplitClassDistribution ValidationDistribution { get; init; } = new();
    public SplitClassDistribution TestDistribution { get; init; } = new();

    public double TrainRatio { get; init; }
    public double ValidationRatio { get; init; }
    public double TestRatio { get; init; }

    public long ElapsedMs { get; init; }
    public long PeakMemoryMB { get; init; }
}

/// <summary>Dataset v1: deterministik stratified split (train/val/test) + metadata. Feature Store TEK kaynak.</summary>
public sealed record Dataset
{
    public required IReadOnlyList<DatasetSample> Train { get; init; }
    public required IReadOnlyList<DatasetSample> Validation { get; init; }
    public required IReadOnlyList<DatasetSample> Test { get; init; }
    public required DatasetMetadata Metadata { get; init; }
}
