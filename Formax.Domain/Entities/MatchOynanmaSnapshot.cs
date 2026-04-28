namespace Formax.Domain.Entities
{
    /// <summary>
    /// FORMAX — Oynanma Snapshot (Backtest & history)
    ///
    /// Amaç:
    /// - OynanmaSkoru'nun maç öncesi / maç başı anlık görüntüsünü saklamak.
    /// - Backtest, "maç öncesi snapshot" ile yapılır.
    ///
    /// Not:
    /// - Bu bir bahis/tahmin verisi değildir.
    /// - Sadece çoğunluğun oynanma yoğunluğunun ölçümü (0..100).
    /// </summary>
    public class MatchOynanmaSnapshot
    {
        public int Id { get; set; }
        public int MatchId { get; set; }

        public int OynanmaSkoru { get; set; } // 0..100 (50 = Denge)

        public DateTime CapturedAtUtc { get; set; }

        // opsiyonel: "LiveProvider" / "Manual" vs.
        public string? Source { get; set; }
    }
}
