using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Historical.Dataset;
using Microsoft.Extensions.Logging;
using DatasetV1 = Formax.Infrastructure.Historical.Dataset.Dataset;

namespace Formax.Infrastructure.Historical.Prediction;

/// <summary>Eğitim sonucu: model + kullanılan Dataset metadata + süre/epoch bilgisi.</summary>
public sealed record ModelTrainingResult
{
    public required ProbabilityModel Model { get; init; }
    public required DatasetMetadata DatasetMetadata { get; init; }
    public int EpochsRun { get; init; }
    public long ElapsedMs { get; init; }
}

/// <summary>
/// Probability Engine model eğitimi sözleşmesi. TEK veri kaynağı = Dataset v1 (<see cref="IDatasetBuilder"/>,
/// yalnız Feature Store okur). Deterministik + tekrar-eğitilebilir.
/// </summary>
public interface IModelTrainer
{
    Task<ModelTrainingResult> TrainAsync(ModelTrainingOptions? options = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Multinomial logistic regression (softmax) eğiticisi — bağımlılıksız, deterministik. Yalnız Dataset v1'i
/// (train + validation) kullanır; Feature Store dışında veri okumaz. Ağırlıklar sıfırdan başlar, full-batch
/// gradient descent sabit sırayla ilerler → aynı options + aynı dataset AYNI modeli üretir. Validation
/// eğitimde kullanılır: en iyi val log-loss'lu ağırlıklar saklanır (early stopping).
/// </summary>
public sealed class ModelTrainer : IModelTrainer
{
    private const int Classes = 3; // 0=H, 1=D, 2=A

    private readonly IDatasetBuilder _datasetBuilder;
    private readonly ILogger<ModelTrainer> _logger;

    public ModelTrainer(IDatasetBuilder datasetBuilder, ILogger<ModelTrainer> logger)
    {
        _datasetBuilder = datasetBuilder;
        _logger = logger;
    }

    public async Task<ModelTrainingResult> TrainAsync(ModelTrainingOptions? options = null, CancellationToken cancellationToken = default)
    {
        var opt = options ?? ModelTrainingOptions.Default;
        var sw = Stopwatch.StartNew();

        // ── TEK kaynak: Dataset v1 (Feature Store'dan). Historical'a gidilmez. ──
        var dataset = await _datasetBuilder.BuildAsync(cancellationToken).ConfigureAwait(false);
        var (model, epochsRun) = Train(dataset, opt, cancellationToken);
        sw.Stop();

        var valMetrics = model.ValidationMetrics;
        _logger.LogInformation(
            "Model eğitildi: {N} train / {V} val, {E} epoch, {Ms} ms. Val acc={Acc:P2} (baseline {Base:P2}), val logloss={LL:0.0000} (baseline {BLL:0.0000})",
            dataset.Train.Count, dataset.Validation.Count, epochsRun, sw.ElapsedMilliseconds,
            valMetrics?.Accuracy ?? 0, valMetrics?.MajorityBaselineAccuracy ?? 0,
            valMetrics?.LogLoss ?? 0, valMetrics?.BaselineLogLoss ?? 0);

        return new ModelTrainingResult
        {
            Model = model,
            DatasetMetadata = dataset.Metadata,
            EpochsRun = epochsRun,
            ElapsedMs = sw.ElapsedMilliseconds
        };
    }

    /// <summary>
    /// VERİLMİŞ bir Dataset v1 üzerinde LR modelini eğitir (senkron çekirdek). Model Selection çerçevesi
    /// bunu kullanır: tüm adaylar AYNI dataset örneğini paylaşır. Deterministik. Dataset'i kendisi OKUMAZ
    /// → çağıran yalnız Feature Store'dan üretilmiş Dataset v1'i geçmelidir.
    /// </summary>
    public (ProbabilityModel model, int epochsRun) Train(DatasetV1 dataset, ModelTrainingOptions opt, CancellationToken cancellationToken = default)
    {
        var f = dataset.Metadata.FeatureCount;
        if (dataset.Train.Count == 0) throw new InvalidOperationException("Dataset v1 train boş — model eğitilemez.");

        var (xTrainRaw, yTrain) = ToArrays(dataset.Train);
        var (xValRaw, yVal) = ToArrays(dataset.Validation);

        // Standardizasyon parametreleri YALNIZ train'den (val/test sızıntısı yok).
        var (mean, std) = opt.Standardize ? ComputeStandardizer(xTrainRaw, f) : (Zeros(f), Ones(f));
        var xTrain = Standardize(xTrainRaw, mean, std);
        var xVal = Standardize(xValRaw, mean, std);

        // Sınıf öncelikleri (baseline + bilgi) — train dağılımından.
        var priors = ClassPriors(yTrain);

        // Ağırlıklar sıfırdan (deterministik).
        var w = new double[Classes][];
        for (var c = 0; c < Classes; c++) w[c] = new double[f];
        var b = new double[Classes];

        double[][] bestW = Clone(w);
        var bestB = (double[])b.Clone();
        var bestValLoss = double.PositiveInfinity;
        var noImprove = 0;
        var epochsRun = 0;
        var n = xTrain.Length;

        for (var epoch = 1; epoch <= opt.Epochs; epoch++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            epochsRun = epoch;

            // ── Full-batch gradient (deterministik; sabit örnek sırası) ──
            var gw = new double[Classes][];
            for (var c = 0; c < Classes; c++) gw[c] = new double[f];
            var gb = new double[Classes];

            for (var i = 0; i < n; i++)
            {
                var xi = xTrain[i];
                var p = Forward(w, b, xi, f);
                for (var c = 0; c < Classes; c++)
                {
                    var diff = p[c] - (yTrain[i] == c ? 1d : 0d);
                    gb[c] += diff;
                    var gwc = gw[c];
                    for (var j = 0; j < f; j++) gwc[j] += diff * xi[j];
                }
            }

            // Ortalama + L2, ardından güncelle.
            for (var c = 0; c < Classes; c++)
            {
                b[c] -= opt.LearningRate * (gb[c] / n);
                var wc = w[c]; var gwc = gw[c];
                for (var j = 0; j < f; j++)
                {
                    var grad = gwc[j] / n + opt.L2Regularization * wc[j];
                    wc[j] -= opt.LearningRate * grad;
                }
            }

            // ── Validation ile early stopping ──
            if (xVal.Length > 0 && (epoch % opt.EvalEveryEpochs == 0 || epoch == opt.Epochs))
            {
                var valLoss = LogLoss(w, b, xVal, yVal, f);
                if (valLoss + 1e-9 < bestValLoss)
                {
                    bestValLoss = valLoss;
                    bestW = Clone(w);
                    bestB = (double[])b.Clone();
                    noImprove = 0;
                }
                else if (opt.EarlyStoppingPatience > 0 && ++noImprove >= opt.EarlyStoppingPatience)
                {
                    _logger.LogInformation("Model eğitimi early-stop @epoch {E} (en iyi val logloss={L:0.0000})", epoch, bestValLoss);
                    break;
                }
            }
            else if (xVal.Length == 0)
            {
                bestW = Clone(w); bestB = (double[])b.Clone();
            }
        }

        var trainMetrics = Evaluate(bestW, bestB, xTrain, yTrain, f, priors);
        var valMetrics = xVal.Length > 0 ? Evaluate(bestW, bestB, xVal, yVal, f, priors) : null;

        var model = new ProbabilityModel
        {
            TrainedAtUtc = DateTime.UtcNow,
            FeatureCount = f,
            FeatureNames = dataset.Metadata.FeatureNames,
            Mean = mean, Std = std,
            Weights = bestW, Bias = bestB,
            Hyperparameters = opt,
            TrainMetrics = trainMetrics,
            ValidationMetrics = valMetrics
        };

        return (model, epochsRun);
    }

    // ── Yardımcılar ──

    private static (double[][] x, int[] y) ToArrays(IReadOnlyList<DatasetSample> samples)
    {
        var x = new double[samples.Count][];
        var y = new int[samples.Count];
        for (var i = 0; i < samples.Count; i++) { x[i] = samples[i].Features; y[i] = samples[i].Label; }
        return (x, y);
    }

    private static (double[] mean, double[] std) ComputeStandardizer(double[][] x, int f)
    {
        var mean = new double[f]; var std = new double[f];
        if (x.Length == 0) { return (mean, Ones(f)); }
        foreach (var xi in x) for (var j = 0; j < f; j++) mean[j] += xi[j];
        for (var j = 0; j < f; j++) mean[j] /= x.Length;
        foreach (var xi in x) for (var j = 0; j < f; j++) { var d = xi[j] - mean[j]; std[j] += d * d; }
        for (var j = 0; j < f; j++) { std[j] = Math.Sqrt(std[j] / x.Length); if (std[j] <= 1e-12) std[j] = 1d; }
        return (mean, std);
    }

    private static double[][] Standardize(double[][] x, double[] mean, double[] std)
    {
        var f = mean.Length;
        var result = new double[x.Length][];
        for (var i = 0; i < x.Length; i++)
        {
            var row = new double[f];
            var xi = x[i];
            for (var j = 0; j < f; j++) row[j] = std[j] > 0 ? (xi[j] - mean[j]) / std[j] : 0d;
            result[i] = row;
        }
        return result;
    }

    private static double[] Forward(double[][] w, double[] b, double[] x, int f)
    {
        var logits = new double[Classes];
        for (var c = 0; c < Classes; c++)
        {
            double z = b[c]; var wc = w[c];
            for (var j = 0; j < f; j++) z += wc[j] * x[j];
            logits[c] = z;
        }
        return ProbabilityModel.Softmax(logits);
    }

    private static double LogLoss(double[][] w, double[] b, double[][] x, int[] y, int f)
    {
        double sum = 0;
        for (var i = 0; i < x.Length; i++)
        {
            var p = Forward(w, b, x[i], f);
            sum += -Math.Log(Math.Clamp(p[y[i]], 1e-15, 1d));
        }
        return x.Length == 0 ? 0 : sum / x.Length;
    }

    private static double[] ClassPriors(int[] y)
    {
        var priors = new double[Classes];
        foreach (var yi in y) priors[yi]++;
        if (y.Length > 0) for (var c = 0; c < Classes; c++) priors[c] /= y.Length;
        return priors;
    }

    private static ModelMetrics Evaluate(double[][] w, double[] b, double[][] x, int[] y, int f, double[] priors)
    {
        var n = x.Length;
        int correct = 0;
        double logloss = 0, brier = 0, baseLogloss = 0;
        var perClassTotal = new int[Classes];
        var perClassHit = new int[Classes];

        var majorityClass = 0; for (var c = 1; c < Classes; c++) if (priors[c] > priors[majorityClass]) majorityClass = c;
        int majorityHits = 0;

        for (var i = 0; i < n; i++)
        {
            var p = Forward(w, b, x[i], f);
            var pred = 0; for (var c = 1; c < Classes; c++) if (p[c] > p[pred]) pred = c;
            var actual = y[i];

            perClassTotal[actual]++;
            if (pred == actual) { correct++; perClassHit[actual]++; }
            if (majorityClass == actual) majorityHits++;

            logloss += -Math.Log(Math.Clamp(p[actual], 1e-15, 1d));
            baseLogloss += -Math.Log(Math.Clamp(priors[actual], 1e-15, 1d));
            for (var c = 0; c < Classes; c++) { var t = actual == c ? 1d : 0d; brier += (p[c] - t) * (p[c] - t); }
        }

        var recall = new double[Classes];
        for (var c = 0; c < Classes; c++) recall[c] = perClassTotal[c] == 0 ? 0 : perClassHit[c] / (double)perClassTotal[c];

        return new ModelMetrics
        {
            Samples = n,
            Accuracy = n == 0 ? 0 : correct / (double)n,
            LogLoss = n == 0 ? 0 : logloss / n,
            Brier = n == 0 ? 0 : brier / n,
            BaselineLogLoss = n == 0 ? 0 : baseLogloss / n,
            MajorityBaselineAccuracy = n == 0 ? 0 : majorityHits / (double)n,
            PerClassRecall = recall
        };
    }

    private static double[][] Clone(double[][] w)
    {
        var c = new double[w.Length][];
        for (var i = 0; i < w.Length; i++) c[i] = (double[])w[i].Clone();
        return c;
    }

    private static double[] Zeros(int f) => new double[f];
    private static double[] Ones(int f) { var a = new double[f]; for (var j = 0; j < f; j++) a[j] = 1d; return a; }
}
