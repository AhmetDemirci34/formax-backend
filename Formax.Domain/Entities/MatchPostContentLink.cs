using System;

namespace Formax.Domain.Entities
{
    /// <summary>
    /// MAÇ ↔ MAÇ SONRASI HABER BAĞI — EMEKLİ (02.09.2026).
    ///
    /// NEDEN HÂLÂ BURADA: bitmiş maç ekranı artık haber GÖSTERMİYOR (ürün kararı), bu
    /// yüzden bu tabloyu YAZAN ve OKUYAN kod kaldırıldı. Ama tablonun kendisi ve içindeki
    /// satırlar SİLİNMEDİ — bir tabloyu düşürmek geri alınamaz bir işlemdir ve bu iş
    /// kapsamında istenmemiştir.
    ///
    /// Tip bu yüzden korunuyor: EF modelden çıkarılırsa bir sonraki migration tabloyu
    /// DÜŞÜRMEK ister. Yani bu sınıf, veriyi yanlışlıkla yok etmemek için duruyor.
    ///
    /// Yeni kod bu tabloyu KULLANMAMALIDIR. Maç sonrası içerik yolu artık
    /// <see cref="MatchVideo"/> üzerindedir.
    /// </summary>
    public sealed class MatchPostContentLink
    {
        public long Id { get; set; }

        /// <summary>Kanonik Match.Id.</summary>
        public int MatchId { get; set; }

        /// <summary>Kaynak makalenin içerik parmak izi (MatchNewsArticles.ContentHash).</summary>
        public string ContentHash { get; set; } = string.Empty;

        /// <summary>Eski kategori kodu (MatchReport | CoachReaction | …). Artık üretilmez.</summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>0-100 — eşleşmenin gücü.</summary>
        public int Confidence { get; set; }

        /// <summary>Eşleşmenin insan okuyabilir gerekçesi.</summary>
        public string MatchReason { get; set; } = string.Empty;

        /// <summary>Makalenin yayın anı.</summary>
        public DateTime PublishedAtUtc { get; set; }

        /// <summary>Bağın kurulduğu an.</summary>
        public DateTime VerifiedAtUtc { get; set; }
    }
}
