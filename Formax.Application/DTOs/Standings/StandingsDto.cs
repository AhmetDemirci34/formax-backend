namespace Formax.Application.DTOs.Standings
{
    // ─── Provider result models (returned by ISportsDataProvider) ─────────────

    public class SportsStandingEntry
    {
        public int TeamId { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public int Position { get; set; }
        public int Played { get; set; }
        public int Won { get; set; }
        public int Drawn { get; set; }
        public int Lost { get; set; }
        public int GoalsFor { get; set; }
        public int GoalsAgainst { get; set; }
        public int Points { get; set; }

        /// <summary>Last 5 results string, e.g. "WWDLW".</summary>
        public string Form { get; set; } = string.Empty;
    }

    public class SportsCompetitionContext
    {
        /// <summary>"League" | "Cup" | "Knockout"</summary>
        public string CompetitionType { get; set; } = "League";

        /// <summary>Stage/round label, e.g. "Regular Season - 25" or "Quarter-Final".</summary>
        public string StageName { get; set; } = string.Empty;

        /// <summary>League/competition name from the provider.</summary>
        public string LeagueName { get; set; } = string.Empty;
    }

    // ─── MatchDetail DTO section models (consumed by the frontend) ────────────

    /// <summary>Compact standing entry for a single team — used in the match detail response.</summary>
    public class TeamStandingDto
    {
        public int Position { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public int Played { get; set; }
        public int Won { get; set; }
        public int Drawn { get; set; }
        public int Lost { get; set; }
        public int GoalsFor { get; set; }
        public int GoalsAgainst { get; set; }
        public int GoalDifference { get; set; }
        public int Points { get; set; }
        public string Form { get; set; } = string.Empty;
        public bool IsHighlighted { get; set; }   // true for home/away team rows
    }

    /// <summary>
    /// Tek bir ligin TAM puan durumu tablosu (sıralama backend'den gelir; tüketici yeniden
    /// sıralamaz). Avrupa kupası / kupa maçlarında iki takım farklı liglerde olabildiği için
    /// bir maça birden fazla tablo bağlanabilir — bkz. <see cref="StandingSectionDto.Tables"/>.
    /// </summary>
    public class StandingTableDto
    {
        public int LeagueId { get; set; }

        /// <summary>Ligin gerçek adı (Matches.League) — üretilmez, depodan okunur. Yoksa boş.</summary>
        public string LeagueName { get; set; } = string.Empty;

        public int SeasonYear { get; set; }

        /// <summary>Ligdeki TÜM takımlar, Position ARTAN. Kırpılmaz.</summary>
        public List<TeamStandingDto> Rows { get; set; } = new();
    }

    public class StandingSectionDto
    {
        public int LeagueId { get; set; }
        public int SeasonYear { get; set; }

        /// <summary>Ligin gerçek adı (Matches.League). Yoksa boş.</summary>
        public string LeagueName { get; set; } = string.Empty;

        /// <summary>Full standing entry for the home team — null if not found.</summary>
        public TeamStandingDto? HomeTeamPeek { get; set; }

        /// <summary>Full standing entry for the away team — null if not found.</summary>
        public TeamStandingDto? AwayTeamPeek { get; set; }

        /// <summary>
        /// Maçın ilgili olduğu TAM lig tablosu (birincil tablo = <see cref="Tables"/>[0]).
        /// ESKİDEN kırpılmış "slice" idi (ilk 3 + takımların ±2 komşusu, en fazla 10 satır);
        /// kullanıcı ligin tamamını göremiyordu. Alan adı geri-uyum için korundu, içerik
        /// artık TAM tablodur.
        /// </summary>
        public List<TeamStandingDto> TableSlice { get; set; } = new();

        /// <summary>
        /// Gösterilecek tablolar. Ulusal lig maçında TEK tablo (maçın ligi, iki takım da
        /// vurgulu). Avrupa kupası/kupa maçında maçın kendi ligi için puan durumu YOKTUR;
        /// bu durumda takımların KENDİ ulusal lig tabloları döner (ör. Fenerbahçe → Süper Lig,
        /// Lyon → Ligue 1). Hiç veri yoksa liste boştur.
        /// </summary>
        public List<StandingTableDto> Tables { get; set; } = new();
    }

    public class CompetitionContextSectionDto
    {
        /// <summary>"League" | "Cup" | "Knockout"</summary>
        public string CompetitionType { get; set; } = "League";

        public string StageName { get; set; } = string.Empty;
        public string ContextHeadline { get; set; } = string.Empty;
        public string ContextSummary { get; set; } = string.Empty;

        /// <summary>Null unless CompetitionType is Cup or Knockout.</summary>
        public string? BracketJson { get; set; }
    }
}
