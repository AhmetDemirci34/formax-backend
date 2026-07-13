using System;

namespace Formax.Domain.Entities
{
    /// <summary>Canonical FORMAX oyuncu entity'si (GDP tarafından beslenir).</summary>
    public class Player
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? Position { get; set; }
        public string? Nationality { get; set; }
        public DateTime LastUpdatedUtc { get; set; }
    }
}
