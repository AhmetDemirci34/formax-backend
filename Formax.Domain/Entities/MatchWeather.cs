using System;

namespace Formax.Domain.Entities
{
    /// <summary>Canonical FORMAX hava durumu entity'si (GDP tarafından beslenir).</summary>
    public class MatchWeather
    {
        public int Id { get; set; }
        public string? FormaxMatchId { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? TemperatureC { get; set; }
        public string? Condition { get; set; }
        public DateTimeOffset? ForecastUtc { get; set; }
        public DateTime LastUpdatedUtc { get; set; }
    }
}
