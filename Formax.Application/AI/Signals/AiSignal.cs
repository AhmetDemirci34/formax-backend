using System.Collections.Generic;

namespace Formax.Application.AI.Signals
{
    /// <summary>
    /// Çok-kaynak füzyonunun çelişki durumu (Conflict Management).
    /// </summary>
    public enum SignalConflictStatus
    {
        /// <summary>Tek kaynak veya çelişki yok.</summary>
        None = 0,
        /// <summary>2+ kaynak uyumlu → tek sinyalde birleştirildi.</summary>
        Merged = 1,
        /// <summary>2+ kaynak çelişti → resmi/güvenilir+taze kaynak ile çözüldü.</summary>
        Resolved = 2,
        /// <summary>2+ kaynak çelişti, çözecek resmi/otorite kaynak yok.</summary>
        Unresolved = 3,
        /// <summary>Bayat/düşük-güven kaynak baskılandı (fusion'a alınmadı).</summary>
        Suppressed = 4
    }

    /// <summary>
    /// FORMAX GDP — AI Signal Factory çıktısı: bir futbol anlamına dönüştürülmüş TEK sinyal.
    /// Ham veri (JSON/haber/tweet/RSS) DEĞİL; kaynaklar füzyonlanmış, çelişki çözülmüş, kanıtlı sinyal.
    /// MarketProbabilityEngine gelecekte YALNIZ bu sinyalleri (UnifiedMatchAiContext) okuyacak.
    ///
    /// Immutable (record + init). Standart zarf: her sinyal aynı zorunlu kalite alanlarını taşır.
    /// </summary>
    public sealed record AiSignal
    {
        /// <summary>Sinyal adı: AttackStrength, InjuryImpact, OfficialAnnouncement …</summary>
        public string Name { get; init; } = "";

        /// <summary>Kaynak kategorisi: "ApiFootball" | "News" | "Social" | "Fused" | "Derived".</summary>
        public string Category { get; init; } = "";

        /// <summary>Sinyalin doğal büyüklüğü (0..1 normalize veya doğal birim).</summary>
        public double Value { get; init; }

        /// <summary>Yönlü etki (-1..+1): pozitif = ev sahibi lehine, negatif = deplasman lehine.
        /// Yönsüz (severity) sinyallerde 0..1.</summary>
        public double Impact { get; init; }

        /// <summary>Sinyal güveni (0-100) — ZORUNLU kalite alanı.</summary>
        public int Confidence { get; init; }

        /// <summary>Kanıt gücü (0-100) — ZORUNLU kalite alanı.</summary>
        public int EvidenceScore { get; init; }

        /// <summary>En güvenilir kaynağın kalite skoru (0-100) — ZORUNLU kalite alanı.</summary>
        public int SourceTrust { get; init; }

        /// <summary>Tazelik (0..1): 1=çok taze, 0=bayat — ZORUNLU kalite alanı.</summary>
        public double Freshness { get; init; }

        /// <summary>Sinyal veri tamlığı (0..1) — ZORUNLU kalite alanı (envelope kalite skoru).</summary>
        public double DataQuality { get; init; }

        /// <summary>Çok-kaynak çelişki durumu (Conflict Management).</summary>
        public SignalConflictStatus ConflictStatus { get; init; } = SignalConflictStatus.None;

        /// <summary>Sinyali besleyen en yeni kaynağın zamanı (ISO-8601 UTC). Yoksa null.</summary>
        public string? Timestamp { get; init; }

        /// <summary>Açıklanabilir gerekçe (hangi kaynaklardan, çelişki var mı).</summary>
        public string Reason { get; init; } = "";

        /// <summary>İlişkili varlıklar (takım/lig/oyuncu adları).</summary>
        public IReadOnlyList<string> RelatedEntities { get; init; } = new List<string>();

        /// <summary>Gerçek veriyle doldu mu? false → motor bu sinyali yok sayar (fake YOK).</summary>
        public bool HasData { get; init; }
    }
}
