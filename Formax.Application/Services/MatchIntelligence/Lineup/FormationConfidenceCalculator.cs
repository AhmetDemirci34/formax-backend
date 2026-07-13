namespace Formax.Application.Services.MatchIntelligence.Lineup;

/// <summary>
/// Takım (diziliş) güveni. Kaynak, geçmiş veri miktarı, kadro dışı başlayanlar ve eksik hat
/// sayısına göre hesaplanır. Oyuncu güveni PlayerSelectionEngine'de başlama sıklığından gelir.
/// </summary>
public static class FormationConfidenceCalculator
{
    /// <param name="source">Official | Predicted | Insufficient</param>
    /// <param name="snapshotCount">Bulunan geçmiş kadro sayısı</param>
    /// <param name="selectedCount">Seçilen oyuncu sayısı (ideal 11)</param>
    /// <param name="unavailableStarters">Son 11'de olup şimdi kadro dışı olan oyuncu sayısı</param>
    public static int Team(string source, int snapshotCount, int selectedCount, int unavailableStarters)
    {
        if (source == "Official") return 92;
        if (source == "Insufficient" || snapshotCount == 0) return 0;

        // Taban: veri arttıkça güven artar (5 maç → 80).
        var conf = Math.Min(80, 30 + snapshotCount * 10);
        conf -= unavailableStarters * 5;          // kadro dışı belirsizlik ekler
        conf -= Math.Max(0, 11 - selectedCount) * 8; // eksik hat güveni düşürür
        return Math.Clamp(conf, 15, 92);
    }

    public static string Level(int confidence)
        => confidence >= 70 ? "Yüksek" : confidence >= 45 ? "Orta" : confidence > 0 ? "Düşük" : "Yok";
}
