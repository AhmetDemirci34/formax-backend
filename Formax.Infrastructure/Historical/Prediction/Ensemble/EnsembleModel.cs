using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Infrastructure.Historical.Prediction.Selection;

namespace Formax.Infrastructure.Historical.Prediction.Ensemble;

/// <summary>Birden çok modelin olasılıklarını tek Probability çıktısına birleştiren ensemble sözleşmesi.</summary>
public interface IProbabilityEnsemble
{
    IReadOnlyList<string> ModelNames { get; }
    IReadOnlyList<double> Weights { get; }
    /// <summary>Tek birleşik olasılık [H, D, A] (toplam = 1). Deterministik.</summary>
    double[] PredictProba(double[] features);
}

/// <summary>
/// Soft-voting ensemble: her alt-model olasılık üretir, ağırlıklı ortalama alınır → tek Probability çıktısı.
/// Alt-modeller heterojen olabilir (LR, LightGBM, ... hepsi <see cref="ITrainedModel"/>). Ağırlıklar toplamı 1'e
/// normalize edilir; her alt-olasılık toplamı 1 olduğundan birleşik çıktı da 1'e toplanır (savunmacı normalize).
/// Deterministik (saf ağırlıklı toplam). Yeni model = listeye yeni ITrainedModel; yapı değişmez.
/// </summary>
public sealed class EnsembleModel : IProbabilityEnsemble
{
    private const int Classes = 3;

    private readonly IReadOnlyList<ITrainedModel> _models;
    private readonly double[] _weights;

    public EnsembleModel(IReadOnlyList<ITrainedModel> models, double[] weights)
    {
        if (models is null || models.Count == 0) throw new ArgumentException("Ensemble en az bir model ister.", nameof(models));
        if (weights is null || weights.Length != models.Count) throw new ArgumentException("Ağırlık sayısı model sayısına eşit olmalı.", nameof(weights));
        _models = models;
        _weights = NormalizeWeights(weights);
    }

    /// <summary>Alt-modeller (kaydetme için).</summary>
    public IReadOnlyList<ITrainedModel> Models => _models;

    public IReadOnlyList<string> ModelNames => _models.Select(m => m.Name).ToList();
    public IReadOnlyList<double> Weights => _weights;

    public double[] PredictProba(double[] features) => PredictWithComponents(features).combined;

    /// <summary>
    /// Birleşik olasılık + her alt-modelin olasılığı. Confidence Engine bunu kullanır: alt-model UYUMU
    /// (agreement) yalnız birleşik olasılıktan geri çıkarılamaz → aynı probability farklı confidence alabilir.
    /// </summary>
    public (double[] combined, double[][] perModel) PredictWithComponents(double[] features)
    {
        var per = new double[_models.Count][];
        var acc = new double[Classes];
        for (var i = 0; i < _models.Count; i++)
        {
            var p = _models[i].PredictProba(features);
            per[i] = p;
            var w = _weights[i];
            for (var c = 0; c < Classes; c++) acc[c] += w * p[c];
        }

        var sum = acc[0] + acc[1] + acc[2];
        if (sum <= 0) acc = new[] { 1d / Classes, 1d / Classes, 1d / Classes };
        else for (var c = 0; c < Classes; c++) acc[c] /= sum;
        return (acc, per);
    }

    private static double[] NormalizeWeights(double[] w)
    {
        var s = w.Sum();
        if (s <= 0) return Enumerable.Repeat(1d / w.Length, w.Length).ToArray();
        return w.Select(x => x / s).ToArray();
    }
}
