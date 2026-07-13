using System;

namespace Formax.Domain.Entities
{
    /// <summary>Canonical FORMAX stat/mekan entity'si (GDP tarafından beslenir).</summary>
    public class Venue
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? City { get; set; }
        public string? Country { get; set; }
        public int? Capacity { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public DateTime LastUpdatedUtc { get; set; }
    }
}
