using System;
using System.Collections.Generic;

namespace Formax.Infrastructure.Historical.Prediction.Explainability;

/// <summary>Bir feature'ın tahmine (tahmin edilen sınıfa) katkısı — occlusion ile ölçülür.</summary>
public sealed record FeatureContribution
{
    public int Index { get; init; }
    public required string Name { get; init; }
    /// <summary>Feature'ın bu örnekteki gerçek değeri.</summary>
    public double Value { get; init; }
    /// <summary>
    /// İşaretli katkı (olasılık uzayında): P(c*|x) − P(c*|x, feature=baseline). Pozitif = feature tahmin edilen
    /// sonucu YUKARI itti (destek); negatif = zayıflattı.
    /// </summary>
    public double Contribution { get; init; }
    public double AbsContribution => Math.Abs(Contribution);
}

/// <summary>Tek bir tahminin açıklaması: olasılık + en etkili feature'lar + katkılar + insan-okunur metin.</summary>
public sealed record PredictionExplanation
{
    public int PredictedClass { get; init; }
    /// <summary>Tahmin edilen sonuç: H/D/A.</summary>
    public required string PredictedOutcome { get; init; }
    public required double[] Probability { get; init; }
    public required IReadOnlyList<FeatureContribution> TopFeatures { get; init; }
    public required string Explanation { get; init; }
    /// <summary>Kullanılan açıklama yöntemi (şeffaflık).</summary>
    public required string Method { get; init; }
}

/// <summary>Global feature önem sıralamasında tek satır.</summary>
public sealed record FeatureImportanceItem
{
    public int Index { get; init; }
    public required string Name { get; init; }
    /// <summary>Örneklem üzerinde ortalama |katkı| (olasılık uzayı).</summary>
    public double MeanAbsContribution { get; init; }
}

/// <summary>Bir örneklem üzerinde global feature önemi (ortalama |katkı| ile sıralı).</summary>
public sealed record GlobalFeatureImportance
{
    public required IReadOnlyList<FeatureImportanceItem> Ranking { get; init; }
    public int SampleCount { get; init; }
    public required string Method { get; init; }
}

/// <summary>
/// Explainability artifact'ı (serileştirilebilir). Occlusion için gereken BASELINE (eğitim-ortalaması feature
/// değerleri) + feature isimleri + yöntem. Model artifact'ine açıklama desteği bununla eklenir; deterministik.
/// </summary>
public sealed record ExplainerModel
{
    public string Version { get; init; } = "v1";
    /// <summary>Kullanılan bilimsel yöntem (uydurma değil — şeffaf).</summary>
    public string Method { get; init; } = "occlusion-baseline-ablation";
    public int TopK { get; init; } = 8;
    /// <summary>Occlusion baseline: feature başına eğitim-ortalaması. Uzunluk = feature sayısı.</summary>
    public double[] Baseline { get; init; } = Array.Empty<double>();
    public IReadOnlyList<string> FeatureNames { get; init; } = Array.Empty<string>();
}
