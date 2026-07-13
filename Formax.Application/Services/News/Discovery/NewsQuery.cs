using System;
using System.Collections.Generic;

namespace Formax.Application.Services.News.Discovery
{
    /// <summary>
    /// FORMAX Data Engine v2 — bir maça ait haber arama bağlamı. Search Query Builder
    /// tarafından üretilir; provider'lar bu sorgu setini açık kaynaklarda arar.
    /// </summary>
    public sealed class NewsQuery
    {
        public string FormaxMatchId { get; set; } = "";
        public string HomeTeam { get; set; } = "";
        public string AwayTeam { get; set; } = "";
        public string League { get; set; } = "";
        public string Country { get; set; } = "";
        public DateTime KickoffUtc { get; set; }

        /// <summary>Dinamik üretilen arama sorguları (öncelik sırasına göre).</summary>
        public List<string> Queries { get; set; } = new();
    }
}
