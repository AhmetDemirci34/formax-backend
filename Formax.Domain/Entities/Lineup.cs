using System;

namespace Formax.Domain.Entities
{
    /// <summary>Canonical FORMAX kadro (lineup) entity'si (GDP tarafından beslenir).</summary>
    public class Lineup
    {
        public int Id { get; set; }
        public string FormaxMatchId { get; set; } = string.Empty;
        public string? TeamName { get; set; }
        public string? Formation { get; set; }
        public string? Players { get; set; }
        public DateTime LastUpdatedUtc { get; set; }
    }
}
