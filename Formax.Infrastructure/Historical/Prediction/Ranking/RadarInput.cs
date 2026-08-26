using Formax.Infrastructure.Historical.Features;
using Formax.Infrastructure.Historical.Prediction.Confidence;

namespace Formax.Infrastructure.Historical.Prediction.Ranking;

/// <summary>
/// Radar Score motorunun KAYNAK-AGNOSTİK girdi sözleşmesi. Motor, bu verinin nereden geldiğini (Feature Store,
/// canlı fikstür, backtest) bilmez → yalnız içeriğe bağımlıdır (SOLID). Probability + Confidence + yapılandırılmış
/// feature vektörü birlikte taşınır. Deterministik skorlama için yeterli tüm sinyal kaynağı buradadır.
/// </summary>
public sealed record RadarInput
{
    public required int MatchId { get; init; }

    /// <summary>Ensemble olasılığı [H, D, A] (toplam 1).</summary>
    public required double[] Probability { get; init; }

    /// <summary>Confidence değerlendirmesi. NOT: puan olarak DEĞİL, yalnız güvenilirlik çarpanı olarak kullanılır.</summary>
    public required ConfidenceAssessment Confidence { get; init; }

    /// <summary>Yapılandırılmış feature vektörü (form, gol, Elo, H2H, lig gücü — sinyallerin kaynağı).</summary>
    public required MatchFeatureVector Features { get; init; }
}
