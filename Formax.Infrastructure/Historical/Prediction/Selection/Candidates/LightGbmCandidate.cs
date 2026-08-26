using System;
using System.IO;
using System.Linq;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers.LightGbm;
using Microsoft.ML.Transforms;
using DatasetV1 = Formax.Infrastructure.Historical.Dataset.Dataset;

namespace Formax.Infrastructure.Historical.Prediction.Selection.Candidates;

/// <summary>
/// Gerçek LightGBM adayı (Microsoft.ML, native). Multiclass softmax → sınıf olasılıkları. Yalnız verilmiş
/// Dataset v1'i kullanır. DETERMİNİZM: MLContext sabit seed + trainer <c>NumberOfThreads=1</c> + <c>Seed</c>.
/// Etiket ByValue key sıralamasıyla map edilir → <c>Score</c> indeksleri 0=H, 1=D, 2=A ile hizalı.
/// </summary>
public sealed class LightGbmCandidate : IModelCandidate
{
    private const int Seed = 42;

    public string Name => "LightGBM";

    public ITrainedModel Train(DatasetV1 dataset)
    {
        var f = dataset.Metadata.FeatureCount;
        var ml = new MLContext(seed: Seed);

        var trainRows = dataset.Train
            .Select(s => new Row { Features = ToFloat(s.Features), Label = s.Label })
            .ToList();

        // Feature vektör boyutunu şemada sabitler (LightGBM sabit-uzunluk ister; sihirli sayı yok).
        var schemaDef = SchemaDefinition.Create(typeof(Row));
        schemaDef[nameof(Row.Features)].ColumnType = new VectorDataViewType(NumberDataViewType.Single, f);
        var trainView = ml.Data.LoadFromEnumerable(trainRows, schemaDef);

        var options = new LightGbmMulticlassTrainer.Options
        {
            LabelColumnName = "Label",
            FeatureColumnName = "Features",
            NumberOfThreads = 1,        // determinizm
            Seed = Seed,                // determinizm
            NumberOfIterations = 100,
            LearningRate = 0.1,
            NumberOfLeaves = 31,
            MinimumExampleCountPerLeaf = 20
        };

        var pipeline = ml.Transforms.Conversion.MapValueToKey(
                outputColumnName: "Label", inputColumnName: "Label",
                keyOrdinality: ValueToKeyMappingEstimator.KeyOrdinality.ByValue)
            .Append(ml.MulticlassClassification.Trainers.LightGbm(options));

        var model = pipeline.Fit(trainView);

        // Artifact = ML.NET zip byte'ları (hem boyut metriği hem kalıcılaştırma için).
        var artifact = SaveToBytes(ml, model, trainView.Schema);

        // Giriş şeması eğitimdekiyle aynı olmalı (Features sabit-uzunluk 59), aksi halde bind hatası.
        var engine = ml.Model.CreatePredictionEngine<Row, Prediction>(model, inputSchemaDefinition: schemaDef);
        return new Trained(engine, artifact);
    }

    public ITrainedModel Load(byte[] artifact)
    {
        var ml = new MLContext(seed: Seed);
        var tmp = Path.Combine(Path.GetTempPath(), $"lgbm_load_{Guid.NewGuid():N}.zip");
        try
        {
            File.WriteAllBytes(tmp, artifact);
            var model = ml.Model.Load(tmp, out _);
            var f = Formax.Infrastructure.Historical.Dataset.FeatureFlattener.Count;
            var schemaDef = SchemaDefinition.Create(typeof(Row));
            schemaDef[nameof(Row.Features)].ColumnType = new VectorDataViewType(NumberDataViewType.Single, f);
            var engine = ml.Model.CreatePredictionEngine<Row, Prediction>(model, inputSchemaDefinition: schemaDef);
            return new Trained(engine, artifact);
        }
        finally { try { File.Delete(tmp); } catch { /* temizlik başarısızsa yut */ } }
    }

    private static byte[] SaveToBytes(MLContext ml, ITransformer model, DataViewSchema schema)
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"lgbm_{Guid.NewGuid():N}.zip");
        try { ml.Model.Save(model, schema, tmp); return File.ReadAllBytes(tmp); }
        finally { try { File.Delete(tmp); } catch { /* temizlik başarısızsa yut */ } }
    }

    private static float[] ToFloat(double[] x)
    {
        var r = new float[x.Length];
        for (var i = 0; i < x.Length; i++) r[i] = (float)x[i];
        return r;
    }

    private sealed class Row
    {
        public float[] Features = Array.Empty<float>();
        public float Label;
    }

    private sealed class Prediction
    {
        public float[] Score = Array.Empty<float>();
    }

    private sealed class Trained : ITrainedModel
    {
        private readonly PredictionEngine<Row, Prediction> _engine;
        private readonly byte[] _artifact;
        private readonly object _gate = new();

        public Trained(PredictionEngine<Row, Prediction> engine, byte[] artifact)
        {
            _engine = engine;
            _artifact = artifact;
        }

        public string Name => "LightGBM";
        public long ModelSizeBytes => _artifact.Length;
        public byte[] Serialize() => _artifact;

        public double[] PredictProba(double[] features)
        {
            var row = new Row { Features = ToFloat(features), Label = 0 };
            Prediction pred;
            lock (_gate) { pred = _engine.Predict(row); } // PredictionEngine thread-safe değil

            var s = pred.Score;
            var probs = new double[3];
            double sum = 0; var anyNeg = false;
            for (var i = 0; i < 3 && i < s.Length; i++) { probs[i] = s[i]; if (s[i] < 0) anyNeg = true; sum += s[i]; }

            // LightGBM multiclass Score = olasılıklar; savunmacı normalize (bozuksa softmax).
            if (anyNeg || sum <= 0) return ProbabilityModel.Softmax(probs);
            for (var i = 0; i < 3; i++) probs[i] /= sum;
            return probs;
        }
    }
}
