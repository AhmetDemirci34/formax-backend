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

        /// <summary>
        /// Bu maç için harcanabilecek sorgu bütçesi (provider başına). Kickoff'a yakın maç
        /// daha fazla açı tarar, uzaktaki maç daha az → aynı HTTP bütçesi maça yaklaşan
        /// maçlara kayar. 0 = provider kendi varsayılanını kullanır.
        /// </summary>
        public int QueryBudget { get; set; }

        /// <summary>Kickoff'a yakın maç (flash pencere) — provider daha agresif tarar.</summary>
        public bool IsUrgent { get; set; }

        /// <summary>
        /// Aramanın dil/bölge kodu ("en" | "tr"). Türk takımlarının gerçek haberi Türkçe
        /// yayıncılarda çıkar; İngilizce-only arama bu kaynakları hiç görmüyordu.
        /// </summary>
        public string Locale { get; set; } = "en";
    }
}
