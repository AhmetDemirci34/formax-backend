namespace Formax.Application.DTOs.Standings
{
    /// <summary>
    /// api-football <c>/teams/statistics</c> sonucunun sağlayıcı-nötr taşıyıcısı.
    /// Yalnız prediction için faydalı, gerçek sezon toplamları. Coverage yoksa provider
    /// null döner → ingestion satır yazmaz → tüketici HasData=false.
    /// </summary>
    public sealed class SportsTeamStatistics
    {
        /// <summary>Provider EXTERNAL takım id'si (api-football).</summary>
        public int TeamId { get; set; }
        public string TeamName { get; set; } = string.Empty;

        public int PlayedTotal { get; set; }
        public int PlayedHome { get; set; }
        public int PlayedAway { get; set; }
        public int WinsTotal { get; set; }
        public int DrawsTotal { get; set; }
        public int LosesTotal { get; set; }

        public double GoalsForAvgTotal { get; set; }
        public double GoalsForAvgHome { get; set; }
        public double GoalsForAvgAway { get; set; }
        public double GoalsAgainstAvgTotal { get; set; }
        public double GoalsAgainstAvgHome { get; set; }
        public double GoalsAgainstAvgAway { get; set; }

        public int CleanSheetTotal { get; set; }
        public int FailedToScoreTotal { get; set; }

        public string Form { get; set; } = string.Empty;
    }
}
