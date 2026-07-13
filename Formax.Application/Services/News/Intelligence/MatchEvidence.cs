using System;
using System.Collections.Generic;

namespace Formax.Application.Services.News.Intelligence
{
    /// <summary>
    /// FORMAX Data Engine v2.1 — bir maça ait tek dijital kanıt (signal-typed).
    /// Ham haber değil; sinyale dönüştürülmüş, kaynak kalitesiyle güçlendirilmiş kayıt.
    /// </summary>
    public sealed class MatchEvidence
    {
        public string FormaxMatchId { get; set; } = "";
        public string Type { get; set; } = "";       // birincil sinyal (Injury, Transfer…)
        public string Cluster { get; set; } = "";     // tüm sinyaller (virgüllü)
        public string Source { get; set; } = "";      // en güvenilir yayıncı
        public int SourceQuality { get; set; }
        public int Confidence { get; set; }
        public DateTime PublishedUtc { get; set; }
        public string Headline { get; set; } = "";
        public string ContentHash { get; set; } = "";
    }

    /// <summary>
    /// FORMAX Data Engine v2.1 — Match Intelligence Context. Reasoning'e GÖNDERİLEN tek
    /// sindirilmiş çıktı: ham haber değil; Evidence + Signals + Confidence + Clusters +
    /// Top/Latest headlines. (Mevcut Reasoning bunu tüketecek; bu fazda Reasoning değişmez.)
    /// </summary>
    public sealed class MatchIntelligenceContext
    {
        public string FormaxMatchId { get; set; } = "";
        public int TotalEvidence { get; set; }
        public int TotalProviders { get; set; }

        /// <summary>Sinyal türü → adet (Injury:3, Transfer:7 …).</summary>
        public Dictionary<string, int> Signals { get; set; } = new();
        public Dictionary<string, int> Clusters { get; set; } = new();

        public int Confidence { get; set; }
        public List<string> TopHeadlines { get; set; } = new();
        public List<string> LatestHeadlines { get; set; } = new();

        /// <summary>Güven sırasına göre en güçlü kanıtlar (Reasoning detayı için).</summary>
        public List<MatchEvidence> TopEvidence { get; set; } = new();
    }
}
