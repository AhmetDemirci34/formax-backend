using System;

namespace Formax.Domain.Entities
{
    /// <summary>Canonical FORMAX ceza/men (suspension) entity'si (GDP tarafından beslenir).</summary>
    public class Suspension
    {
        public int Id { get; set; }
        public string PlayerName { get; set; } = string.Empty;
        public string? TeamName { get; set; }
        public string? Reason { get; set; }
        public int? Matches { get; set; }
        public DateTime LastUpdatedUtc { get; set; }
    }
}
