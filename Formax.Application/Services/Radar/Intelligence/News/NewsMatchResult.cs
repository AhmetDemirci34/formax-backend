using System;
using System.Collections.Generic;

namespace Formax.Application.Services.Radar.Intelligence.News
{
    /// <summary>
    /// Radar News Intelligence (R.10.1) — result of linking one news item to matches:
    /// the teams it mentions and the matches those teams play in.
    /// </summary>
    public sealed class NewsMatchResult
    {
        public string NewsRawId { get; init; } = string.Empty;
        public string SourceKey { get; init; } = string.Empty;
        public DateTime OccurredAtUtc { get; init; }

        public IReadOnlyList<NewsMention> Mentions { get; init; } = new List<NewsMention>();

        /// <summary>Matches linked via the mentioned teams.</summary>
        public IReadOnlyList<int> MatchIds { get; init; } = new List<int>();

        public bool HasLinks => MatchIds.Count > 0;
    }
}
