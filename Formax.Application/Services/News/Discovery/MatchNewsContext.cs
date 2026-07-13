using System;
using System.Collections.Generic;

namespace Formax.Application.Services.News.Discovery
{
    /// <summary>
    /// FORMAX Data Engine v2 — duplikasyon sonrası tekilleştirilmiş haber. Aynı hikâye
    /// N yayıncıda olabilir; burada tek kayda iner, kaynaklar/küme/güven birleşir.
    /// </summary>
    public sealed class DedupedNewsItem
    {
        public string FormaxMatchId { get; set; } = "";
        public string Headline { get; set; } = "";
        public string Summary { get; set; } = "";
        public string Url { get; set; } = "";
        public DateTime PublishedUtc { get; set; }
        public string Language { get; set; } = "en";

        /// <summary>Bu hikâyeyi yayınlayan kaynaklar (yayıncı + arama provider'ı).</summary>
        public List<string> Sources { get; set; } = new();
        public int SourceCount { get; set; }

        /// <summary>Çok-etiketli kümeler (Injury, Transfer, Lineup …).</summary>
        public List<string> Clusters { get; set; } = new();

        public int Confidence { get; set; }

        /// <summary>Tekilleştirme anahtarı (içerik hash). Depolamada benzersizlik için.</summary>
        public string ContentHash { get; set; } = "";
    }

    /// <summary>
    /// FORMAX Data Engine v2 — bir maçın haber özeti (Reasoning Engine'in tüketeceği çıktı).
    /// </summary>
    public sealed class MatchNewsContext
    {
        public string FormaxMatchId { get; set; } = "";
        public int TotalNews { get; set; }
        public int TotalProviders { get; set; }

        /// <summary>Küme → haber sayısı (Injury: 4, Transfer: 7 …).</summary>
        public Dictionary<string, int> Clusters { get; set; } = new();

        /// <summary>En güçlü (güven sırası) başlıklar.</summary>
        public List<string> TopHeadlines { get; set; } = new();

        /// <summary>En yeni başlıklar.</summary>
        public List<string> LatestHeadlines { get; set; } = new();

        /// <summary>Toplu haber güveni (0–100).</summary>
        public int Confidence { get; set; }
    }
}
