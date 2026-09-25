using System;

namespace Formax.Domain.Entities
{
    /// <summary>
    /// ORGANİZASYON × MARKET × KESİM TARİHİ DEĞERLENDİRMESİ — eklemeli ve değişmez. Aynı (organizasyon, market, kesim, model,
    /// config, politika, mod) ikinci kez yazılmaz. RawGateStatus o pencerenin matematiksel sonucudur; PublishedStateBefore/After
    /// yalnız Publish modunda doldurulur ve kullanıcıya gösterilen kararlı durumun o penceredeki geçişidir.
    /// </summary>
    public sealed class MarketEligibilityEvaluation
    {
        public long Id { get; set; }
        public int OrganizationId { get; set; }
        public string MarketFamily { get; set; } = string.Empty;
        public DateTime EvaluationCutoffUtc { get; set; }
        public string ModelVersion { get; set; } = string.Empty;
        public string ModelRunId { get; set; } = string.Empty;
        public string ConfigHash { get; set; } = string.Empty;
        /// <summary>Yayın politikası sürümü (eligibility-publication-N).</summary>
        public string PolicyVersion { get; set; } = string.Empty;
        /// <summary>Ham kapı politikası sürümü (market-eligibility-N) — eşikler buradadır, yayın politikası değiştirmez.</summary>
        public string GatePolicyVersion { get; set; } = string.Empty;
        /// <summary>EvaluateOnly | Publish.</summary>
        public string Mode { get; set; } = string.Empty;
        /// <summary>Scheduled | Manual | BootstrapReplay.</summary>
        public string Source { get; set; } = string.Empty;
        public int SampleCount { get; set; }
        public double LogLoss { get; set; }
        public double Brier { get; set; }
        public double Ece { get; set; }
        public double? CalibrationSlope { get; set; }
        public double? CalibrationIntercept { get; set; }
        public double BaselineLogLoss { get; set; }
        public double DifferenceFromBaseline { get; set; }
        public double ConfidenceIntervalLow { get; set; }
        public double ConfidenceIntervalHigh { get; set; }
        public double Bias { get; set; }
        public double Coverage { get; set; }
        /// <summary>Mevcut kapının kendi kararı (Eligible | Limited | InsufficientSample | ...).</summary>
        public string GateStatus { get; set; } = string.Empty;
        /// <summary>PASS | FAIL | HARD_FAIL.</summary>
        public string RawGateStatus { get; set; } = string.Empty;
        public string RawGateReasonsJson { get; set; } = "[]";
        public DateTime EvaluatedAtUtc { get; set; }
        public string? PublishedStateBefore { get; set; }
        public string? PublishedStateAfter { get; set; }
        public string? TransitionReason { get; set; }
        public string? PublicationRunKey { get; set; }
    }

    /// <summary>
    /// GÜNCEL YAYIN DURUMU — hücre başına tek satır (Closed | PendingOpen | Open | PendingClose). Her değişimde StateVersion artar ve
    /// <see cref="MarketEligibilityStateTransition"/> defterine önceki/sonraki durum + gerekçe eklenir (geri dönüş buradan yapılır).
    /// </summary>
    public sealed class MarketEligibilityState
    {
        public long Id { get; set; }
        public int OrganizationId { get; set; }
        public string MarketFamily { get; set; } = string.Empty;
        public string PublishedState { get; set; } = string.Empty;
        public int StateVersion { get; set; }
        public string PolicyVersion { get; set; } = string.Empty;
        public string? ModelVersion { get; set; }
        public string? ConfigHash { get; set; }
        public DateTime? LastEvaluationCutoffUtc { get; set; }
        public string? LastRawGateStatus { get; set; }
        public string? LastTransitionReason { get; set; }
        public string? LastPublicationRunKey { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }

    /// <summary>Yayın durumu değişim defteri — eklemeli; bootstrap, haftalık yayın ve geri dönüş aynı defterde.</summary>
    public sealed class MarketEligibilityStateTransition
    {
        public long Id { get; set; }
        public int OrganizationId { get; set; }
        public string MarketFamily { get; set; } = string.Empty;
        public string? FromState { get; set; }
        public string ToState { get; set; } = string.Empty;
        public int StateVersion { get; set; }
        public string ReasonCode { get; set; } = string.Empty;
        public string PublicationRunKey { get; set; } = string.Empty;
        public DateTime? EvaluationCutoffUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    /// <summary>
    /// YAYIN TURU — bootstrap / haftalık yayın / yalnız değerlendirme / geri dönüş. RunKey tekildir: aynı hafta ikinci yayın,
    /// ikinci bootstrap ya da aynı geri dönüş yazılamaz. Süre, bellek ve sorgu ölçümleri denetim için saklanır.
    /// </summary>
    public sealed class MarketEligibilityPublicationRun
    {
        public long Id { get; set; }
        public string RunKey { get; set; } = string.Empty;
        /// <summary>Bootstrap | Publish | EvaluateOnly | Rollback.</summary>
        public string Mode { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public DateTime? EvaluationCutoffUtc { get; set; }
        public string? WeekKey { get; set; }
        public string ModelVersion { get; set; } = string.Empty;
        public string? ConfigHash { get; set; }
        public string PolicyVersion { get; set; } = string.Empty;
        /// <summary>Bootstrap: içeri alınan yayımlanmış koşu (PredictionModelRuns.RunId).</summary>
        public string? SourceModelRunId { get; set; }
        public DateTime StartedAtUtc { get; set; }
        public DateTime CompletedAtUtc { get; set; }
        public long DurationMs { get; set; }
        public long PeakWorkingSetBytes { get; set; }
        public int Cells { get; set; }
        public int Transitions { get; set; }
        public int VisibilityChanges { get; set; }
        public string SummaryJson { get; set; } = "{}";
    }
}
