using System;
using System.Collections.Generic;

namespace Formax.Application.Services.News.Discovery
{
    /// <summary>
    /// FORMAX Data Engine v2 — bir haber provider'ının döndürdüğü tek, normalize edilmiş
    /// haber adayı. Her provider kendi ham şemasını buna çevirir → motor tek tip görür.
    /// </summary>
    public sealed class NewsCandidate
    {
        public string Headline { get; set; } = "";
        public string Summary { get; set; } = "";
        public string Content { get; set; } = "";
        public string Url { get; set; } = "";
        public DateTime PublishedUtc { get; set; }
        public string Provider { get; set; } = "";   // arama provider'ı (ör. Google News)
        public string Publisher { get; set; } = "";   // asıl yayıncı (ör. ESPN) — varsa
        public string Language { get; set; } = "en";
        public List<string> Teams { get; set; } = new();
        public string League { get; set; } = "";
        public string Country { get; set; } = "";
        public int Confidence { get; set; }
        public string FormaxMatchId { get; set; } = "";
    }
}
