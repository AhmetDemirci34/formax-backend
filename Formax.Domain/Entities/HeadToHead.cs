using System;

namespace Formax.Domain.Entities
{
    /// <summary>Canonical FORMAX ikili karşılaşma (H2H) entity'si (GDP tarafından beslenir).</summary>
    public class HeadToHead
    {
        public int Id { get; set; }
        public string FormaxMatchId { get; set; } = string.Empty;
        public string? HomeTeam { get; set; }
        public string? AwayTeam { get; set; }
        public string? Summary { get; set; }
        public DateTime LastUpdatedUtc { get; set; }
    }
}
