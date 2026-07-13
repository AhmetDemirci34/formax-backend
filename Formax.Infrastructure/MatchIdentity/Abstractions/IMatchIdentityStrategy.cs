namespace Formax.Infrastructure.MatchIdentity.Abstractions;

/// <summary>
/// Tek bir eşleştirme kriteri: bir aday referansın var olan bir kimlikle aynı maçı temsil edip
/// etmediğine dair KENDİ skorunu (katkısını) üretir. Engine tüm kriter skorlarını TOPLAYARAK
/// toplam Confidence'ı hesaplar ve eşiğe göre karar verir.
/// </summary>
public interface IMatchIdentityStrategy
{
    /// <summary>Kriter adı (iz/teşhis için).</summary>
    string Name { get; }

    /// <summary>
    /// Bu kriterin, adayın mevcut kimlikle aynı maç olduğuna dair skor katkısı (0 = katkı yok).
    /// Skorlar toplandığından her kriterin kendi ağırlığı vardır.
    /// </summary>
    double Score(ProviderMatchReference candidate, MatchIdentityResult existing);
}
