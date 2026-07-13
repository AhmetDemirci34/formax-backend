using System;

namespace Formax.Domain.Entities
{
    /// <summary>Canonical FORMAX transfer entity'si (GDP tarafından beslenir).</summary>
    public class Transfer
    {
        public int Id { get; set; }
        public string PlayerName { get; set; } = string.Empty;
        public string? FromTeam { get; set; }
        public string? ToTeam { get; set; }
        public string? Fee { get; set; }
        public DateTimeOffset? TransferDateUtc { get; set; }
        public DateTime LastUpdatedUtc { get; set; }
    }
}
