using System;

namespace Formax.Domain.Entities
{
    /// <summary>Canonical FORMAX hakem entity'si (GDP tarafından beslenir).</summary>
    public class Referee
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? Nationality { get; set; }
        public DateTime LastUpdatedUtc { get; set; }
    }
}
