namespace Formax.Domain.Entities
{
    /// <summary>
    /// FORMAX GDP — api-football <c>/teams/statistics</c> sezon özeti (takım-başına).
    /// Sezon boyu GERÇEK toplu istatistik: standings'ten daha zengin (ev/deplasman gol
    /// ortalamaları, clean-sheet, gol atamama, form). Prediction bağlamını besler.
    ///
    /// Composite PK: (LeagueId, SeasonYear, TeamId) — LeagueStanding ile AYNI şema.
    /// <see cref="TeamId"/> = provider EXTERNAL takım id'si (api-football), Team.ExternalTeamId
    /// ile eşlenir (standings/player-status ile aynı kimlik kuralı). Coverage yoksa satır yazılmaz
    /// → tüketici HasData=false döner (fake YOK).
    ///
    /// Günde bir WorldPerceptionDailyJob (04:00 UTC) tarafından yenilenir.
    /// </summary>
    public class TeamSeasonStatistic
    {
        /// <summary>Internal league ID — Match.LeagueId ile eşleşir (external lig id).</summary>
        public int LeagueId { get; set; }

        /// <summary>Sezon başlangıç yılı (2024-25 için 2024).</summary>
        public int SeasonYear { get; set; }

        /// <summary>Provider EXTERNAL takım id'si — Team.ExternalTeamId ile eşlenir.</summary>
        public int TeamId { get; set; }

        public string TeamName { get; set; } = string.Empty;

        // ── Fixtures (oynanan / galibiyet / beraberlik / mağlubiyet) ─────────────
        public int PlayedTotal { get; set; }
        public int PlayedHome { get; set; }
        public int PlayedAway { get; set; }
        public int WinsTotal { get; set; }
        public int DrawsTotal { get; set; }
        public int LosesTotal { get; set; }

        // ── Goals (maç-başı ortalama; provider hesaplar) ─────────────────────────
        public double GoalsForAvgTotal { get; set; }
        public double GoalsForAvgHome { get; set; }
        public double GoalsForAvgAway { get; set; }
        public double GoalsAgainstAvgTotal { get; set; }
        public double GoalsAgainstAvgHome { get; set; }
        public double GoalsAgainstAvgAway { get; set; }

        // ── Türetilmiş güven sinyalleri ──────────────────────────────────────────
        public int CleanSheetTotal { get; set; }
        public int FailedToScoreTotal { get; set; }

        /// <summary>Sezon form dizisi, ör. "WWDLW".</summary>
        public string Form { get; set; } = string.Empty;

        public DateTime UpdatedAt { get; set; }
    }
}
