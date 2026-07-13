namespace Formax.Application.DTOs.Fixtures;

/// <summary>
/// Normalised fixture record returned by ISportsDataProvider.GetFixturesAsync.
/// One record per fixture from the external provider.
/// Used exclusively by FixtureSyncJob — not exposed to the API layer.
/// </summary>
public class SportsFixtureResult
{
    /// <summary>External fixture id — dedup key for Match upsert.</summary>
    public string ExternalMatchId { get; set; } = string.Empty;

    public DateTime MatchDate { get; set; }

    /// <summary>
    /// Normalised status string.
    /// Values: "NotStarted" | "Live" | "Finished" | "Postponed" | "Cancelled"
    /// </summary>
    public string Status { get; set; } = "NotStarted";

    public string LeagueName { get; set; } = string.Empty;

    /// <summary>api-football league id — stored as Match.LeagueId for standings lookup.</summary>
    public int LeagueExternalId { get; set; }

    public string Round { get; set; } = string.Empty;

    // ── Score (provider-supplied; null when not played / not reported) ──────────
    // Only consumed for Finished fixtures — FixtureSyncJob never overwrites live
    // scores (Locked Decision #6). Null for upcoming fixtures.
    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }

    // ── Home team ──────────────────────────────────────────────────────────────

    /// <summary>External team id — dedup key for Team upsert.</summary>
    public string HomeTeamExternalId { get; set; } = string.Empty;

    public string HomeTeamName { get; set; } = string.Empty;

    public string? HomeLogoUrl { get; set; }

    // ── Away team ──────────────────────────────────────────────────────────────

    public string AwayTeamExternalId { get; set; } = string.Empty;

    public string AwayTeamName { get; set; } = string.Empty;

    public string? AwayLogoUrl { get; set; }

    // ── Match venue & official ─────────────────────────────────────────────────

    /// <summary>Referee name — null when not provided by the provider.</summary>
    public string? Referee { get; set; }

    /// <summary>Venue / stadium name — null when not provided by the provider.</summary>
    public string? Venue { get; set; }
}
