using System;

namespace Formax.Domain.Entities
{
    public sealed class MatchSapmaSnapshot
    {
        public int MatchId { get; set; }

        public int Sapma { get; set; }
        public string Bolge { get; set; } = string.Empty;
        public bool SessizMi { get; set; }

        public DateTime ComputedAtUtc { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
    }
}