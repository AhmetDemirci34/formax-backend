using System;

namespace Formax.Domain.Entities
{
    /// <summary>Canonical FORMAX maç istatistiği entity'si (GDP tarafından beslenir).</summary>
    public class MatchStatistics
    {
        public int Id { get; set; }
        public string FormaxMatchId { get; set; } = string.Empty;
        public string? TeamName { get; set; }
        public string? Metrics { get; set; }
        public DateTime LastUpdatedUtc { get; set; }
    }
}
