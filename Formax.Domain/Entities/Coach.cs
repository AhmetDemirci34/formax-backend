using System;

namespace Formax.Domain.Entities
{
    /// <summary>Canonical FORMAX teknik direktör entity'si (GDP tarafından beslenir).</summary>
    public class Coach
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? Nationality { get; set; }
        public DateTime LastUpdatedUtc { get; set; }
    }
}
