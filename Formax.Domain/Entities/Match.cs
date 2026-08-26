namespace Formax.Domain.Entities
{
    public class Match
    {
        public int Id { get; set; }

        public int HomeTeamId { get; set; }
        public int AwayTeamId { get; set; }

        public DateTime MatchDate { get; set; }

        /// <summary>
        /// İlk yarı skoru — sağlayıcının `score.halftime` değeri. Nullable ZORUNLU:
        /// null = sağlayıcı vermedi (ör. henüz oynanmadı / eski kayıt), 0 = GERÇEK sıfır.
        /// MS skorundan türetilmez, tahmin edilmez.
        /// </summary>
        public int? HalfTimeHomeScore { get; set; }
        public int? HalfTimeAwayScore { get; set; }

        public int HomeScore { get; set; }
        public int AwayScore { get; set; }

        public string? MatchMinute { get; set; }
        public string Status { get; set; } = "NotStarted";

        public string League { get; set; } = string.Empty;
        public int LeagueId { get; set; }

        // Kanonik Competition bağı (Team gibi FK ile; GDP fikstüründen çözülür). Opsiyonel.
        public int? CompetitionId { get; set; }

        // Kanonik Venue bağı (Competition ile aynı pattern; GDP fikstüründen çözülür). Opsiyonel.
        public int? VenueId { get; set; }

        public DateTime CreatedAt { get; set; }

        public string? LastEmittedEventType { get; set; }

        /// <summary>
        /// External ID used by the sports data provider (e.g. api-football fixture id).
        /// Null when the match has not been mapped to an external source.
        /// </summary>
        public string? ExternalMatchId { get; set; }

        /// <summary>Referee name as provided by the sports data provider. Null until synced.</summary>
        public string? Referee { get; set; }

        /// <summary>Venue / stadium name as provided by the sports data provider. Null until synced.</summary>
        public string? Venue { get; set; }

        /// <summary>
        /// Sağlayıcının verdiği GERÇEK tur/aşama adı ("Regular Season - 1",
        /// "3rd Qualifying Round", "Play-offs", "Round of 16", "Final" …).
        /// Maçın TÜRÜNÜ (lig maçı / eleme / final) bu alan belirler; tahmin edilmez.
        /// Sağlayıcı vermediyse null kalır ve UI tür satırını göstermez.
        /// </summary>
        public string? Round { get; set; }

        // 🔥 GERİ EKLENDİ
        public Match CloneForComparison()
        {
            return (Match)this.MemberwiseClone();
        }

        // 🔥 NEW NAV
        public Team? HomeTeam { get; set; }
        public Team? AwayTeam { get; set; }

        public Competition? Competition { get; set; }

        // GDP kanonik Venue entity bağı. (main'in string? Venue = provider ham ad; bu = canonical FK nav.)
        public Venue? CanonicalVenue { get; set; }
    }
}