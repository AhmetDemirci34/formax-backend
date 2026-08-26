using System;
using System.Collections.Generic;

namespace Formax.Domain.Entities
{
    /// <summary>
    /// SHADOW B — "aynı model + maç öncesi kanıt düzeltmesi" deney hattı. INSERT-ONLY.
    ///
    /// SHADOW A'YA DOKUNMAZ. Shadow A'nın yazdığı <see cref="Prediction"/> satırı bu hattın
    /// GİRDİSİDİR (<see cref="BasePredictionId"/>); okunur, asla değiştirilmez. Model, kapı,
    /// kalibrasyon ve λ formülleri Shadow A'daki hâliyle kalır — B yalnız A'nın YAYIMLADIĞI
    /// olasılığın üzerine, deterministik ve kayıt altına alınabilir bir düzeltme uygular.
    ///
    /// LLM BU SATIRDAKİ HİÇBİR SAYIYI ÜRETMEZ. Yüzde hesabı backend matematiğidir; kanıtın
    /// hangi takıma ait olduğu ve türü, mevcut kural-tabanlı Evidence katmanından gelir.
    ///
    /// Her satır kendi girdilerini taşır (<see cref="BaseHomeProbability"/>,
    /// <see cref="HomeImpact"/>, <see cref="AppliedTilt"/>), böylece yayımlanmış her sayı
    /// satırın kendisinden yeniden hesaplanabilir.
    /// </summary>
    public class ShadowBPrediction
    {
        public long Sequence { get; set; }

        /// <summary>FMXB + 20 hex. Deterministik; aynı girdi → aynı id.</summary>
        public string PredictionId { get; set; } = string.Empty;

        /// <summary>Bu düzeltmenin dayandığı Shadow A tahmini (FMXP…). Değiştirilmez, okunur.</summary>
        public string BasePredictionId { get; set; } = string.Empty;

        /// <summary>Deney hattının adı. Bu tabloda daima NEWS_ADJUSTED.</summary>
        public string Variant { get; set; } = "NEWS_ADJUSTED";

        /// <summary>Production Matches.Id — Shadow A ile AYNI maç kimliği.</summary>
        public int MatchId { get; set; }

        public string? CanonicalMatchId { get; set; }

        /// <summary>Haber/kanıt deposunun kimliği (FMX-…). Kanıt denetimi bununla yapılır.</summary>
        public string FormaxMatchId { get; set; } = string.Empty;

        public DateTime MatchDate { get; set; }
        public DateTime PredictionTimestamp { get; set; }

        /// <summary>
        /// Bu tahmini besleyebilen en son kanıtın anı: Shadow A'nın kanıt kesimi ile
        /// KULLANILAN kanıtların en yenisinin yayım anından geç olanı. Daima maç saatinden küçük.
        /// </summary>
        public DateTime? EvidenceCutoff { get; set; }

        public string ModelVersion { get; set; } = string.Empty;
        public string TeamStrengthVersion { get; set; } = string.Empty;
        public string GateVersion { get; set; } = string.Empty;
        public string CalibrationVersion { get; set; } = string.Empty;

        /// <summary>Düzeltme kuralının sürümü (ör. NEWS_ADJUST_V1).</summary>
        public string AdjustmentVersion { get; set; } = string.Empty;

        // ── Shadow A'nın yayımladığı sayılar (denetim için birebir taşınır) ──────────
        public double? BaseHomeProbability { get; set; }
        public double? BaseDrawProbability { get; set; }
        public double? BaseAwayProbability { get; set; }

        // ── Shadow B'nin yayımladığı sayılar ────────────────────────────────────────
        public double? HomeProbability { get; set; }
        public double? DrawProbability { get; set; }
        public double? AwayProbability { get; set; }

        /// <summary>Shadow A'nın kapı kararı DEĞİŞTİRİLMEDEN devralınır.</summary>
        public bool PredictionEligible { get; set; }
        public string ConfidenceClass { get; set; } = string.Empty;
        public string GateStatus { get; set; } = string.Empty;
        public string GateReason { get; set; } = string.Empty;

        // ── Düzeltmenin girdileri ve çıktısı ────────────────────────────────────────
        /// <summary>Bu tahmine giren (kapılardan geçmiş, maç öncesi) kanıt sayısı.</summary>
        public int EvidenceCount { get; set; }

        /// <summary>Ev sahibi aleyhine biriken kanıt ağırlığı (0 = etki yok).</summary>
        public double HomeImpact { get; set; }

        /// <summary>Deplasman aleyhine biriken kanıt ağırlığı.</summary>
        public double AwayImpact { get; set; }

        /// <summary>Log-odds kaydırması. 0 ise olasılıklar Shadow A ile birebir aynıdır.</summary>
        public double AppliedTilt { get; set; }

        /// <summary>Olasılık gerçekten değişti mi? (tilt ≠ 0)</summary>
        public bool AdjustmentApplied { get; set; }

        /// <summary>İnsan okunur gerekçe: hangi kanıt türü, hangi takım, ne kadar.</summary>
        public string AdjustmentReason { get; set; } = string.Empty;

        public string ContentHash { get; set; } = string.Empty;

        /// <summary>Daima true — Shadow B kullanıcıya gösterilmez.</summary>
        public bool ShadowMode { get; set; } = true;

        public DateTime CreatedAt { get; set; }

        public ShadowBPredictionSettlement? Settlement { get; set; }
        public ICollection<ShadowBPredictionEvidence> Evidence { get; set; } = new List<ShadowBPredictionEvidence>();
    }

    /// <summary>
    /// Bir Shadow B tahminine gerçekten GİREN tek kanıt. Denetlenebilirlik şartı budur:
    /// "hangi habere dayanarak %62 → %58 oldu?" sorusunun cevabı bu tabloda durur.
    /// </summary>
    public class ShadowBPredictionEvidence
    {
        public long Id { get; set; }

        public string PredictionId { get; set; } = string.Empty;

        /// <summary>MatchEvidenceRecords.Id — kanıtın deposundaki kimliği.</summary>
        public int EvidenceId { get; set; }

        /// <summary>Kanıtın tekilleştirme anahtarı (haber gövdesine köprü).</summary>
        public string EvidenceContentHash { get; set; } = string.Empty;

        /// <summary>Sakatlık / Ceza / Kadro / İlk 11.</summary>
        public string EventType { get; set; } = string.Empty;

        /// <summary>Kanıtın ilgili olduğu takım adı (kural-tabanlı çözümleme).</summary>
        public string RelatedTeam { get; set; } = string.Empty;

        /// <summary>HOME | AWAY.</summary>
        public string Side { get; set; } = string.Empty;

        public string Source { get; set; } = string.Empty;
        public int SourceQuality { get; set; }
        public int Confidence { get; set; }
        public int SourceCount { get; set; }

        /// <summary>Kanıtın yayım anı — maç saatinden KÜÇÜK olmak zorunda.</summary>
        public DateTime PublishedUtc { get; set; }

        /// <summary>Bu kanıtın toplam etkiye kattığı ağırlık.</summary>
        public double Weight { get; set; }

        public ShadowBPrediction? Prediction { get; set; }
    }

    /// <summary>
    /// Shadow B tahminine iliştirilen gerçek sonuç. Shadow A ile AYNI gerçeklik kaynağından
    /// (Matches.HomeScore/AwayScore) türetilir — B için ayrı bir "gerçek skor" ÜRETİLMEZ.
    /// </summary>
    public class ShadowBPredictionSettlement
    {
        public string PredictionId { get; set; } = string.Empty;

        public int ActualHomeGoals { get; set; }
        public int ActualAwayGoals { get; set; }

        /// <summary>HomeWin | Draw | AwayWin — Shadow A ile aynı kural.</summary>
        public string ActualResult { get; set; } = string.Empty;

        public DateTime SettlementTimestamp { get; set; }

        public ShadowBPrediction? Prediction { get; set; }
    }
}
