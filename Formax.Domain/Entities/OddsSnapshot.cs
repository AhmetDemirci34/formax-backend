using System;

namespace Formax.Domain.Entities
{
    /// <summary>
    /// Radar Odds Movement (R.11.1) — a single captured odds reading for a match from a
    /// source. Foundation only: no collection in this sprint (no API, no scraper). Rows
    /// are the raw input the movement engine compares.
    /// </summary>
    public sealed class OddsSnapshot
    {
        public long Id { get; set; }

        public int MatchId { get; set; }

        /// <summary>Originating source (Radar SourceDefinition.Id), 0 when synthetic/test.</summary>
        public int SourceId { get; set; }

        public double HomeOdds { get; set; }
        public double DrawOdds { get; set; }
        public double AwayOdds { get; set; }

        public DateTime CapturedAtUtc { get; set; }
    }
}
