namespace Formax.Domain.Entities
{
    /// <summary>
    /// Kanonik puan durumu satırı (competition + takım). GDP tarafından canonical Match
    /// geçmişinden TÜRETİLİR (H2H ile aynı yaklaşım; provider'a bağlı değil). Ada göre
    /// tekilleştirilir — sağlayıcı-bağımsız kalması için (CompetitionName + TeamName) anahtardır.
    /// </summary>
    public class CompetitionStanding
    {
        public int Id { get; set; }

        public string CompetitionName { get; set; } = string.Empty;
        public string TeamName { get; set; } = string.Empty;

        public int Played { get; set; }
        public int Won { get; set; }
        public int Drawn { get; set; }
        public int Lost { get; set; }
        public int GoalsFor { get; set; }
        public int GoalsAgainst { get; set; }
        public int Points { get; set; }
        public int Rank { get; set; }

        public System.DateTime LastUpdatedUtc { get; set; }
    }
}
