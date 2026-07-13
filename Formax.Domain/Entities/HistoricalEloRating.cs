namespace Formax.Domain.Entities
{
    /// <summary>
    /// Tarihsel Elo zaman-serisi kaydı (EloRatings.csv satırı: date/club/country/elo).
    /// Kulüp adı <see cref="HistoricalTeam"/>'e resolver (normalize+alias) ile eşleştirilir;
    /// eşleşmezse <see cref="HistoricalTeamId"/> null kalır (ham club adı yine saklanır).
    /// <see cref="SourceKey"/> = ClubKey|Date ile idempotent tekilleştirilir.
    /// </summary>
    public class HistoricalEloRating
    {
        public int Id { get; set; }

        /// <summary>Çözülen tarihsel takım (eşleşmezse null).</summary>
        public int? HistoricalTeamId { get; set; }

        /// <summary>Ham kulüp adı (CSV'den).</summary>
        public string Club { get; set; } = string.Empty;

        public string? Country { get; set; }

        public System.DateTime Date { get; set; }

        public double Elo { get; set; }

        /// <summary>İdempotent tekilleştirme anahtarı (ClubKey|Date). Benzersiz.</summary>
        public string SourceKey { get; set; } = string.Empty;

        public System.DateTime LastUpdatedUtc { get; set; }
    }
}
