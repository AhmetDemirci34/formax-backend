using System;

namespace Formax.Domain.Entities
{
    /// <summary>
    /// Bir maç için üretilmiş, YAYIMLANMIŞ tahmin. INSERT-ONLY.
    ///
    /// Bu satır bir kez yazıldıktan sonra değişmez. Yeni bir tahmin gerekiyorsa yeni bir satır
    /// yazılır (yeni <see cref="PredictionId"/>, yeni <see cref="Sequence"/>); eski satır olduğu
    /// gibi kalır. Değişmezlik üç katmanda birden korunur:
    ///   1) uygulama: PredictionStore yeniden yazmayı reddeder,
    ///   2) EF: bu entity için UPDATE yolu açılmaz,
    ///   3) veritabanı: DENY UPDATE + AFTER UPDATE trigger.
    ///
    /// <see cref="PredictionEligible"/> false ise üç olasılık da NULL'dur. Sıfır değil, prior
    /// değil, yer tutucu değil — yok.
    /// </summary>
    public class Prediction
    {
        /// <summary>Yayım sırası. "Bu maçın güncel tahmini hangisi?" sorusunun tek doğru cevabı.</summary>
        public long Sequence { get; set; }

        /// <summary>FMXP + 20 hex. SHA256(MatchId | 4 versiyon | EvidenceCutoff | PredictionTimestamp).</summary>
        public string PredictionId { get; set; } = string.Empty;

        /// <summary>Production Matches.Id.</summary>
        public int MatchId { get; set; }

        /// <summary>Canonical FORMAX maç kimliği (FMXM…). Üretimde henüz eşlenmemişse null.</summary>
        public string? CanonicalMatchId { get; set; }

        public DateTime MatchDate { get; set; }
        public DateTime PredictionTimestamp { get; set; }

        /// <summary>Tahmini besleyebilen en son kanıtın tarihi. Daima maç tarihinden küçük.</summary>
        public DateTime? EvidenceCutoff { get; set; }

        public string ModelVersion { get; set; } = string.Empty;
        public string TeamStrengthVersion { get; set; } = string.Empty;
        public string GateVersion { get; set; } = string.Empty;
        public string CalibrationVersion { get; set; } = string.Empty;

        /// <summary>Reddedilmiş tahminde NULL.</summary>
        public double? HomeProbability { get; set; }
        public double? DrawProbability { get; set; }
        public double? AwayProbability { get; set; }

        public bool PredictionEligible { get; set; }

        /// <summary>NONE | LOW | MEDIUMLOW | MEDIUM | HIGH. Olasılıktan türetilmez.</summary>
        public string ConfidenceClass { get; set; } = string.Empty;

        /// <summary>ACCEPTED | REJECTED.</summary>
        public string GateStatus { get; set; } = string.Empty;

        /// <summary>"OK" veya tetiklenen gate kodları, boru işaretiyle ayrılmış.</summary>
        public string GateReason { get; set; } = string.Empty;

        /// <summary>Yayım anındaki parmak izi. Satırın sonradan değişip değişmediği bununla ölçülür.</summary>
        public string ContentHash { get; set; } = string.Empty;

        /// <summary>Gölge modda üretildi mi? true ise kullanıcıya gösterilmez.</summary>
        public bool ShadowMode { get; set; }

        public DateTime CreatedAt { get; set; }

        public PredictionSettlement? Settlement { get; set; }
    }

    /// <summary>
    /// Maç bittikten sonra tahmine iliştirilen gerçek sonuç. AYRI TABLO, 1:1.
    ///
    /// Ayrı olmasının nedeni: settlement tahmini güncelleyen bir işlem DEĞİLDİR. Sonuç kendi
    /// satırında durur, böylece tahmin satırında settlement'ın yazabileceği bir alan bulunmaz.
    /// PK = PredictionId olması "bir tahmin iki kez settle edilemez" kuralını şema düzeyinde uygular.
    /// </summary>
    public class PredictionSettlement
    {
        public string PredictionId { get; set; } = string.Empty;

        public int ActualHomeGoals { get; set; }
        public int ActualAwayGoals { get; set; }

        /// <summary>HomeWin | Draw | AwayWin. Gollerden türetilir, CHECK ile tutarlılığı korunur.</summary>
        public string ActualResult { get; set; } = string.Empty;

        public DateTime SettlementTimestamp { get; set; }

        public Prediction? Prediction { get; set; }
    }
}
