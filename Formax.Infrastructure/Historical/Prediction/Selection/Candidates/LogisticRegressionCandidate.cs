using System.Text;
using System.Text.Json;
using DatasetV1 = Formax.Infrastructure.Historical.Dataset.Dataset;

namespace Formax.Infrastructure.Historical.Prediction.Selection.Candidates;

/// <summary>
/// Baseline aday: mevcut deterministik multinomial logistic regression (<see cref="ModelTrainer"/>).
/// Model Trainer altyapısını BOZMADAN yeniden kullanır (senkron <c>Train</c> çekirdeği). Model boyutu =
/// serileştirilmiş artifact (JSON) byte sayısı.
/// </summary>
public sealed class LogisticRegressionCandidate : IModelCandidate
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ModelTrainer _trainer;
    private readonly ModelTrainingOptions _options;

    public LogisticRegressionCandidate(ModelTrainer trainer)
    {
        _trainer = trainer;
        _options = ModelTrainingOptions.Default;
    }

    public string Name => "LogisticRegression";

    public ITrainedModel Train(DatasetV1 dataset)
    {
        var (model, _) = _trainer.Train(dataset, _options);
        return new Trained(model);
    }

    public ITrainedModel Load(byte[] artifact)
    {
        var model = JsonSerializer.Deserialize<ProbabilityModel>(artifact, Json)
                    ?? throw new System.InvalidOperationException("LR artifact deserialize edilemedi.");
        return new Trained(model);
    }

    private sealed class Trained : ITrainedModel
    {
        private readonly ProbabilityModel _model;
        private readonly byte[] _bytes;
        public Trained(ProbabilityModel model)
        {
            _model = model;
            _bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(model, Json));
        }
        public string Name => "LogisticRegression";
        public long ModelSizeBytes => _bytes.Length;
        public double[] PredictProba(double[] features) => _model.PredictProba(features);
        public byte[] Serialize() => _bytes;
    }
}
