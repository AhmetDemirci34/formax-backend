namespace Formax.Domain.Entities
{
    /// <summary>
    /// Tarihsel veri kümesinin kanonik takım kaydı. Canlı/GDP <see cref="Team"/>'den İZOLE.
    /// <see cref="NormalizedKey"/> ile tekilleştirilir (alias/noktalama/diakritik normalize edilmiş
    /// ad) — böylece "Nott'm Forest" ve "Nottm Forest" tek takıma düşer.
    /// </summary>
    public class HistoricalTeam
    {
        public int Id { get; set; }

        /// <summary>Görüntülenen ad (ilk görülen ham ad).</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Tekilleştirme anahtarı (normalize + alias uygulanmış). Benzersiz.</summary>
        public string NormalizedKey { get; set; } = string.Empty;

        /// <summary>Ülke (EloRatings.csv'den eşleşirse). Opsiyonel.</summary>
        public string? Country { get; set; }

        public System.DateTime LastUpdatedUtc { get; set; }
    }
}
