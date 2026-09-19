using System;

namespace Formax.Domain.Entities
{
    /// <summary>
    /// OLASI SONUÇ SNAPSHOT'I — arka plan tahmin işi tarafından üretilen, değişmez maç tahmini.
    ///
    /// Keşfet ve Maç Detayı AYNI satırı okur (aynı SnapshotId, ModelVersion, yüzdeler, gerekçe kodları ve hesaplama
    /// zamanı). Sayfa açılışı olasılık hesaplamaz. Yeni girdi geldiğinde yeni satır yazılır, eskisi geçmiş olarak kalır
    /// (IsCurrent=false).
    /// </summary>
    public sealed class MatchPredictionSnapshot
    {
        public long Id { get; set; }
        /// <summary>Genel tekil kimlik (ekranlar arası eşleşme için).</summary>
        public string SnapshotId { get; set; } = string.Empty;
        public int MatchId { get; set; }
        public string ModelVersion { get; set; } = string.Empty;
        /// <summary>Kullanılan kalibrasyon koşusunun kimliği (PredictionModelRuns.RunId).</summary>
        public string? CalibrationRunId { get; set; }
        public DateTime ComputedAtUtc { get; set; }
        /// <summary>Girdi kesme anı — bu andan sonra biten maçlar modele girmedi.</summary>
        public DateTime InputsCutoffUtc { get; set; }
        /// <summary>Available | InsufficientData.</summary>
        public string Status { get; set; } = "Available";
        public double? ExpectedHomeGoals { get; set; }
        public double? ExpectedAwayGoals { get; set; }
        public double EvidenceCoverage { get; set; }
        public int HomeSampleSize { get; set; }
        public int AwaySampleSize { get; set; }
        /// <summary>Bütün aileler ve adaylar (JSON).</summary>
        public string PayloadJson { get; set; } = string.Empty;
        /// <summary>Girdi özeti — aynı girdiyle yeni satır yazılmaz.</summary>
        public string InputHash { get; set; } = string.Empty;
        public bool IsCurrent { get; set; }

        // ── 17.09.2026 (additive): uygunluk, yayın durumu ve değişim denetimi ──
        /// <summary>Enabled | Limited | Disabled — kullanıcıya yüzde YALNIZ Enabled'da gösterilir.</summary>
        public string? PredictionEligibility { get; set; }
        /// <summary>Uygunluk kararının gerekçe kodları (JSON dizi).</summary>
        public string? EligibilityReasonsJson { get; set; }
        /// <summary>Published | NeedsReview — NeedsReview satırı kullanıcıya gitmez; önceki yayımlanmış snapshot güncel kalır.</summary>
        public string? PublicationStatus { get; set; }
        public string? PreviousSnapshotId { get; set; }
        /// <summary>Periodic | ModelUpdate | OfficialLineup | Postponed | Cancelled | Suspended | KickoffChanged | VenueChanged | StatusChange.</summary>
        public string? TriggerType { get; set; }
        public string? TriggerSource { get; set; }
        public DateTime? TriggeredAtUtc { get; set; }
        /// <summary>Eski/yeni yüzdeler, farklar, yeni girdiler, izin verilen değişim sınırı (JSON).</summary>
        public string? ChangeAuditJson { get; set; }
        /// <summary>Doğrulanmış maç zekâsı parmak izi (resmî kadro, kritik gelişmeler, durum, başlama saati).</summary>
        public string? IntelligenceFingerprint { get; set; }
        public string? SelectionVersion { get; set; }
        public DateTime? KickoffUtc { get; set; }
    }

    /// <summary>
    /// LİG TAHMİN UYGUNLUĞU — her model koşusunda lig başına bağımsız zamansal sınavın sonucu (Enabled/Limited/Disabled).
    /// Snapshot işi yalnız en son kabul edilmiş koşunun satırlarını okur.
    /// </summary>
    public sealed class LeaguePredictionEligibility
    {
        public long Id { get; set; }
        public string RunId { get; set; } = string.Empty;
        public string ModelVersion { get; set; } = string.Empty;
        public string PolicyVersion { get; set; } = string.Empty;
        public int LeagueId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string ReasonsJson { get; set; } = "[]";
        public int TestMatches { get; set; }
        public double ResultLogLoss { get; set; }
        public double BaselineResultLogLoss { get; set; }
        public double LogLossDiffCiHigh { get; set; }
        public double CalibrationError { get; set; }
        public double HomeBias { get; set; }
        public double DrawBias { get; set; }
        public string MetricsJson { get; set; } = string.Empty;
        public DateTime EvaluatedAtUtc { get; set; }
    }

    /// <summary>
    /// ORGANİZASYON × MARKET AİLESİ UYGUNLUĞU — yayın kararının BİRİNCİ katmanı. Lig anahtarı (yukarıdaki
    /// <see cref="LeaguePredictionEligibility"/>) korunur ama artık tek başına yayını kapatmaz: her market ailesi
    /// kendi sınavından geçer. Model sürümüyle birlikte ÖNCEDEN üretilir; okuma yolu backtest çalıştırmaz.
    /// </summary>
    public sealed class LeagueMarketEligibility
    {
        public long Id { get; set; }
        public string RunId { get; set; } = string.Empty;
        public string ModelVersion { get; set; } = string.Empty;
        public string PolicyVersion { get; set; } = string.Empty;
        public int LeagueId { get; set; }
        /// <summary>MatchResult1X2 | DoubleChance | TotalGoals15 | TotalGoals25 | TotalGoals35 | BothTeamsToScore.</summary>
        public string Family { get; set; } = string.Empty;
        /// <summary>Eligible | Limited | InsufficientSample | WorseThanBaseline | CalibrationFailed | DataQualityFailed.</summary>
        public string Status { get; set; } = string.Empty;
        public string ReasonsJson { get; set; } = "[]";
        public int TestMatches { get; set; }
        public double LogLoss { get; set; }
        public double BaselineLogLoss { get; set; }
        public double Brier { get; set; }
        public double LogLossDiffCiHigh { get; set; }
        public double CalibrationError { get; set; }
        public double MaxBias { get; set; }
        public string MetricsJson { get; set; } = string.Empty;
        public DateTime EvaluatedAtUtc { get; set; }
    }

    /// <summary>
    /// KALICI TAHMİN YENİLEME KUYRUĞU — doğrulanmış yapılandırılmış olay (resmî ilk 11, erteleme, saat değişikliği...) canonical DB'ye
    /// yazıldığı İŞLEMDE eklenir. Aynı olay (DedupeKey) ikinci kez eklenmez; restart sonrası kaybolmaz; kısa debounce (DueAtUtc) sonra işlenir.
    /// </summary>
    public sealed class PredictionRecomputeRequest
    {
        public long Id { get; set; }
        public int MatchId { get; set; }
        public string TriggerType { get; set; } = string.Empty;
        public string TriggerSource { get; set; } = string.Empty;
        public string DedupeKey { get; set; } = string.Empty;
        public DateTime RequestedAtUtc { get; set; }
        public DateTime DueAtUtc { get; set; }
        /// <summary>Pending | Processing | Done | Skipped | Failed.</summary>
        public string Status { get; set; } = "Pending";
        public int Attempts { get; set; }
        public DateTime? LockedUntilUtc { get; set; }
        public DateTime? ProcessedAtUtc { get; set; }
        public string? ResultSnapshotId { get; set; }
        public string? Outcome { get; set; }
    }

    /// <summary>
    /// CANLI TAHMİN KARNESİ — maç başlamadan kilitlenen son yayımlanmış snapshot ve maç bitince sonuç botunun kanonik sonucuyla
    /// otomatik değerlendirmesi. Kilitlendikten sonra tahmin DEĞİŞMEZ.
    /// </summary>
    public sealed class PredictionScorecard
    {
        public long Id { get; set; }
        public int MatchId { get; set; }
        public string SnapshotId { get; set; } = string.Empty;
        public string ModelVersion { get; set; } = string.Empty;
        public int LeagueId { get; set; }
        public string Eligibility { get; set; } = string.Empty;
        public DateTime PredictionCreatedAtUtc { get; set; }
        public DateTime KickoffUtc { get; set; }
        public DateTime LockedAtUtc { get; set; }
        /// <summary>Seçilen üç ana kart (JSON).</summary>
        public string MainCardsJson { get; set; } = "[]";
        /// <summary>Bütün kalibre olasılıklar (JSON).</summary>
        public string ProbabilitiesJson { get; set; } = "{}";
        public int? FinalHomeScore { get; set; }
        public int? FinalAwayScore { get; set; }
        public string? FinalStatus { get; set; }
        public DateTime? SettledAtUtc { get; set; }
        public bool? ResultCardCorrect { get; set; }
        public bool? GoalsCardCorrect { get; set; }
        public bool? BttsCardCorrect { get; set; }
        public double? ResultLogLoss { get; set; }
    }

    /// <summary>Tahmin teşhis kaydı (aşırı değişim, analiz–kart çelişkisi, kaynak yok...). DedupeKey ile tekildir.</summary>
    public sealed class PredictionDiagnostic
    {
        public long Id { get; set; }
        public int MatchId { get; set; }
        public string? SnapshotId { get; set; }
        public string Kind { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string DedupeKey { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
    }

    /// <summary>
    /// MODEL KOŞUSU — zamansal (sızıntısız) geriye dönük test, kalibrasyon parametreleri ve metrikler.
    /// Tahmin işi yalnız en son "Accepted" koşunun parametrelerini kullanır.
    /// </summary>
    public sealed class PredictionModelRun
    {
        public long Id { get; set; }
        public string RunId { get; set; } = string.Empty;
        public string ModelVersion { get; set; } = string.Empty;
        public DateTime StartedAtUtc { get; set; }
        public DateTime CompletedAtUtc { get; set; }
        /// <summary>Accepted | Rejected.</summary>
        public string Status { get; set; } = string.Empty;
        public string ParametersJson { get; set; } = string.Empty;
        public string MetricsJson { get; set; } = string.Empty;
        public int TrainMatches { get; set; }
        public int CalibrationMatches { get; set; }
        public int TestMatches { get; set; }
    }
}
