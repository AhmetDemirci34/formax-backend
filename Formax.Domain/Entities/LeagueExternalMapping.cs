namespace Formax.Domain.Entities
{
    /// <summary>
    /// Maps an internal LeagueId to the external sports data provider's league ID.
    /// Populated by admin or migration — not ingested automatically.
    /// Required for the daily standings refresh to know which external IDs to fetch.
    /// </summary>
    public class LeagueExternalMapping
    {
        /// <summary>Primary key — internal league ID (matches Match.LeagueId).</summary>
        public int LeagueId { get; set; }

        /// <summary>External league ID for the sports data provider (e.g. "39" for EPL on api-football).</summary>
        public string ExternalLeagueId { get; set; } = string.Empty;

        /// <summary>Season start year the mapping is valid for (e.g. 2024).</summary>
        public int SeasonYear { get; set; }

        /// <summary>Human-readable league name stored for debugging.</summary>
        public string LeagueName { get; set; } = string.Empty;
    }
}
