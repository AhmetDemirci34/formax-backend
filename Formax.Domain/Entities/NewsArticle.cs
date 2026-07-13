using System;

namespace Formax.Domain.Entities
{
    /// <summary>Canonical FORMAX haber entity'si (GDP tarafından beslenir).</summary>
    public class NewsArticle
    {
        public int Id { get; set; }
        public string? FormaxMatchId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Source { get; set; }
        public string? Url { get; set; }
        public DateTimeOffset? PublishedUtc { get; set; }
        public DateTime LastUpdatedUtc { get; set; }
    }
}
