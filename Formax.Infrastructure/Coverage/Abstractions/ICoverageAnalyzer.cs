namespace Formax.Infrastructure.Coverage.Abstractions;

/// <summary>
/// Tek bir veri kategorisinin doluluk durumunu üreten analizör.
///
/// Her kategori için bir analizör yazılır; <see cref="CoverageEngine"/> hepsini çalıştırıp
/// tam bir doluluk haritası oluşturur. Kategori-düzeyi gerçek hesaplama (Exists/Score/LastUpdated)
/// bu sözleşmeyi uygulayan somut analizörlerde, sonraki fazda yazılacak.
/// </summary>
public interface ICoverageAnalyzer
{
    /// <summary>Bu analizörün sorumlu olduğu kategori.</summary>
    CoverageCategory Category { get; }

    /// <summary>Kategorinin doluluk durumunu üretir.</summary>
    CoverageItem Analyze(CoverageContext context);
}
