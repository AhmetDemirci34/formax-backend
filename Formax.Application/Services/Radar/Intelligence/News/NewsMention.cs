using System;

namespace Formax.Application.Services.Radar.Intelligence.News
{
    /// <summary>
    /// Radar News Intelligence (R.10.1) — a deterministic detection that a team is
    /// mentioned by a news item (alias substring match or resolved team id).
    /// </summary>
    public sealed class NewsMention
    {
        public int TeamId { get; init; }
        public string TeamName { get; init; } = string.Empty;

        /// <summary>The alias text that matched (or the canonical name).</summary>
        public string MatchedAlias { get; init; } = string.Empty;

        public string SourceKey { get; init; } = string.Empty;
        public string NewsRawId { get; init; } = string.Empty;

        public DateTime OccurredAtUtc { get; init; }
    }
}
