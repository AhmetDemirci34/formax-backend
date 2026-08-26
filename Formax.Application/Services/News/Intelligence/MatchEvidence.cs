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

        /// <summary>
        /// Haberin GERÇEK kısa özeti (provider snippet'i). Ölçüldü (14.08): bu alan zincirde
        /// hiç yoktu — MatchNewsArticles.Summary'de duran gerçek içerik Evidence'a taşınmıyordu,
        /// bu yüzden LLM yalnız başlık görüyor ve başlıktan anlam üretmek zorunda kalıyordu.
        /// Yeni veri kaynağı YOK: aynı haberin zaten kayıtlı özeti okunur (ContentHash ile).
        /// Boş olabilir — o zaman elde gerçekten yalnız başlık vardır.
        /// </summary>
        public string Summary { get; set; } = "";

        /// <summary>
        /// Bu OLAYI doğrulayan gerçek kaynak sayısı. Aynı gelişmeyi 5 yayıncı verdiyse
        /// 5 kanıt değil, KaynakSayısı=5 olan TEK kanıt olur (olay tekilleştirmesi).
        /// </summary>
        public int SourceCount { get; set; } = 1;

        /// <summary>Olayı doğrulayan yayıncılar (tekil).</summary>
        public List<string> Sources { get; set; } = new();

        /// <summary>
        /// Haberin maçla ilişkisi — Backend kararı: "Maç" (iki takım da anılıyor) veya
        /// "Takım" (tek takımın sıradaki maçı). Boşsa ilişki belirlenmemiştir.
        /// </summary>
        public string Relation { get; set; } = "";

        /// <summary>Maça göre zaman konumu: MaçÖncesi / MaçGünü / MaçSonrası / Eski.</summary>
        public string Timing { get; set; } = "";

        /// <summary>Haber hangi takımla ilgili (tek takım haberiyse dolu).</summary>
        public string RelatedTeam { get; set; } = "";

        /// <summary>O takımın bu maçtaki rakibi.</summary>
        public string OpponentTeam { get; set; } = "";

        /// <summary>
        /// Olayın öznesi olan oyuncu — YALNIZ bilinen kadro adıyla eşleştiyse dolu.
        /// Boşsa haberin öznesi bir oyuncu değildir (ya da kesin belirlenemedi).
        /// </summary>
        public string Player { get; set; } = "";

        /// <summary>Olayın öznesi teknik direktör/menajer ise adı (yoksa boş).</summary>
        public string Coach { get; set; } = "";

        /// <summary>
        /// FUTBOL OLAY TÜRÜ — haberin ne anlattığı. Ham sinyal etiketi değil; kullanıcıya
        /// anlatılabilir olay taksonomisi: Transfer / Sakatlık / Ceza / Kadro / İlk11 /
        /// Teknik Direktör Açıklaması / Oyuncu Açıklaması / Kulüp Açıklaması / Maç Önizlemesi /
        /// Maç Raporu / Antrenman / Müsabaka Gelişmesi / Diğer.
        /// </summary>
        public string EventType { get; set; } = "";

        /// <summary>Olayın maç açısından önemi: Yüksek / Orta / Düşük (deterministik).</summary>
        public string Importance { get; set; } = "";
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
