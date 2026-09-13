namespace Formax.Domain.Entities
{
    /// <summary>
    /// AI MAÇ ANALİZİ KAYDI — arka planda kanıttan üretilmiş, doğrulanmış analiz (maç başına tek satır).
    ///
    /// Kullanıcı sayfası YALNIZ bu satırı okur. <see cref="InputHash"/> kanıtın özetidir: sonuç,
    /// kadro ya da puan durumu değişmediyse analiz yeniden üretilmez.
    /// </summary>
    public class MatchAnalysisSnapshot
    {
        public int Id { get; set; }

        public int MatchId { get; set; }

        public DateTime KickoffUtc { get; set; }

        public DateTime GeneratedAtUtc { get; set; }

        /// <summary>Kanıt kümesinin SHA-256 özeti.</summary>
        public string InputHash { get; set; } = string.Empty;

        /// <summary>"Ready" | "InsufficientData" | "SimilarityRejected".</summary>
        public string Status { get; set; } = "Ready";

        /// <summary>"Deterministic" | "DeterministicVariant" | "DeterministicShort" | "LlmVerbalized".</summary>
        public string Generator { get; set; } = "Deterministic";

        /// <summary>Kabul edilen belge (bölümler + cümle başına kanıt anahtarları), JSON.</summary>
        public string ContentJson { get; set; } = string.Empty;

        /// <summary>Kanıt kümesi, JSON.</summary>
        public string EvidenceJson { get; set; } = string.Empty;

        /// <summary>Belgenin düz metni — benzerlik ve tekrar ölçümü.</summary>
        public string FlatText { get; set; } = string.Empty;

        /// <summary>Önceki analizlerle en yüksek 3-gram Jaccard benzerliği.</summary>
        public double MaxSimilarity { get; set; }

        public int? MostSimilarMatchId { get; set; }

        /// <summary>Doğrulayıcının reddettiği cümle sayısı (bu üretimde).</summary>
        public int RejectedSentenceCount { get; set; }

        /// <summary>Ret gerekçeleri, JSON.</summary>
        public string? RejectionsJson { get; set; }

        /// <summary>Bu üretimde yapılan LLM çağrısı (arka plan).</summary>
        public int LlmCalls { get; set; }
    }
}
