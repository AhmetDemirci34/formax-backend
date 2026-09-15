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
