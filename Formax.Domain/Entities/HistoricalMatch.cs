namespace Formax.Domain.Entities
{
    /// <summary>
    /// Tarihsel kanonik maç (Matches.csv satırı). Canlı <see cref="Match"/>'ten İZOLE — 230K tarihsel
    /// kayıt kendi tablosunda; skor + Elo + form + istatistik + odds korunur. <see cref="SourceKey"/>
    /// ile idempotent tekilleştirilir (Division|Date|HomeKey|AwayKey).
    /// </summary>
    public class HistoricalMatch
    {
        public int Id { get; set; }

        public int HistoricalCompetitionId { get; set; }
        public int HomeTeamId { get; set; }
        public int AwayTeamId { get; set; }

        public System.DateTime MatchDate { get; set; }
        public string? MatchTime { get; set; }

        // ── Skorlar ──
        public int? FTHome { get; set; }
        public int? FTAway { get; set; }
        public string? FTResult { get; set; }
        public int? HTHome { get; set; }
        public int? HTAway { get; set; }
        public string? HTResult { get; set; }

        // ── Analitik (Elo + form) ──
        public double? HomeElo { get; set; }
        public double? AwayElo { get; set; }
        public double? Form3Home { get; set; }
        public double? Form5Home { get; set; }
        public double? Form3Away { get; set; }
        public double? Form5Away { get; set; }

        // ── Maç istatistikleri ──
        public int? HomeShots { get; set; }
        public int? AwayShots { get; set; }
        public int? HomeTarget { get; set; }
        public int? AwayTarget { get; set; }
        public int? HomeFouls { get; set; }
        public int? AwayFouls { get; set; }
        public int? HomeCorners { get; set; }
        public int? AwayCorners { get; set; }
        public int? HomeYellow { get; set; }
        public int? AwayYellow { get; set; }
        public int? HomeRed { get; set; }
        public int? AwayRed { get; set; }

        // ── Odds ──
        public double? OddHome { get; set; }
        public double? OddDraw { get; set; }
        public double? OddAway { get; set; }
        public double? MaxHome { get; set; }
        public double? MaxDraw { get; set; }
        public double? MaxAway { get; set; }
        public double? Over25 { get; set; }
        public double? Under25 { get; set; }
        public double? MaxOver25 { get; set; }
        public double? MaxUnder25 { get; set; }
        public double? HandiSize { get; set; }
        public double? HandiHome { get; set; }
        public double? HandiAway { get; set; }

        /// <summary>İdempotent tekilleştirme anahtarı. Benzersiz.</summary>
        public string SourceKey { get; set; } = string.Empty;

        public System.DateTime LastUpdatedUtc { get; set; }
    }
}
