using System;

namespace Formax.Domain.Entities
{
    /// <summary>Canonical FORMAX sakatlık entity'si (GDP tarafından beslenir).</summary>
    public class Injury
    {
        public int Id { get; set; }
        public string PlayerName { get; set; } = string.Empty;
        public string? TeamName { get; set; }
        public string? Reason { get; set; }
        public string? Status { get; set; }
        public DateTime LastUpdatedUtc { get; set; }
    }
}
