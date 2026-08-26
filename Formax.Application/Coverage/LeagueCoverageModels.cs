using System.Collections.Generic;

namespace Formax.Application.Coverage
{
    /// <summary>
    /// GDP Final Evolution — League Coverage Intelligence çıktı modelleri. GDP'nin hangi ligde hangi
    /// verinin ne kadar KALİTELİ olduğunu GERÇEK ingestion sonuçlarından öğrenmesi. Tahmin/hardcode YOK.
    /// Salt-okunur diagnostik + Discovery/Refresh yönetimi için kullanılır. Motor bunu OKUMAZ.
    /// </summary>
    public sealed class CapabilityCoverage
    {
        public string Name { get; init; } = "";
        /// <summary>Supported | Partial | Unavailable — gerçek coverage oranından türetilir.</summary>
        public string Status { get; init; } = "Unavailable";
        /// <summary>0..1 kapsam oranı (veri olan maç/takım / toplam).</summary>
        public double CoverageRatio { get; init; }
        /// <summary>0..1 tazelik (son güncelleme yaşından; yoksa 0).</summary>
        public double Freshness { get; init; }
        /// <summary>0-100 kaynak güveni (varsa; yoksa 0).</summary>
        public int SourceTrust { get; init; }
        /// <summary>0-100 kanıt gücü (varsa; yoksa 0).</summary>
        public int EvidenceScore { get; init; }
        /// <summary>0..1 birleşik veri kalitesi (ratio×freshness×trust harmanı).</summary>
        public double DataQuality { get; init; }
        /// <summary>Kapsam: "PerLeague" (temiz join) | "Global" (iki-dünya/genel).</summary>
        public string Scope { get; init; } = "PerLeague";
        public string Detail { get; init; } = "";
    }

    /// <summary>Tek ligin coverage profili — capability matrisi + genel skor + Discovery tier'ı.</summary>
    public sealed class LeagueCoverage
    {
        public int LeagueId { get; init; }
        public string LeagueName { get; init; } = "";
        public int MatchCount { get; init; }
        public int TeamCount { get; init; }
        public int UpcomingCount { get; init; }

        public IReadOnlyList<CapabilityCoverage> Capabilities { get; init; } = new List<CapabilityCoverage>();

        /// <summary>0-100 genel coverage skoru (capability oranlarının ağırlıklı ortalaması). GLOBAL kapsam:
        /// operasyonel capability'ler ligin TÜM maçlarına (tüm sezonlar/bitmiş dahil) bölünür.</summary>
        public int OverallScore { get; init; }
        /// <summary>Premium | Standard | Limited | Passive — config eşiklerinden (GLOBAL skordan).</summary>
        public string Tier { get; init; } = "Passive";

        // ── OPERATIONAL (aktif pencere) — yalnız yaklaşan maçlar payda alınarak hesaplanır. ──
        /// <summary>Operasyonel capability matrisi (Prediction/Competition/Availability/Live payda = yaklaşan maç).</summary>
        public IReadOnlyList<CapabilityCoverage> OperationalCapabilities { get; init; } = new List<CapabilityCoverage>();
        /// <summary>0-100 operasyonel coverage skoru (yalnız aktif penceredeki maçlara göre). Ligin yaklaşan maçı
        /// yoksa 0 ve OperationalActive=false.</summary>
        public int OperationalScore { get; init; }
        /// <summary>Ligin aktif pencerede (yaklaşan) maçı var mı — Operational readiness bunları kapsar.</summary>
        public bool OperationalActive { get; init; }
    }

    /// <summary>GDP genel hazırlık skorları — coverage'dan türetilir.</summary>
    public sealed class GdpReadiness
    {
        // ── GLOBAL: keşfedilen TÜM lig evreni (api-football'un döndürdüğü her fikstür), TÜM maçlar payda. ──
        public int OverallGdpReadiness { get; init; }
        /// <summary>Motorun GERÇEKTEN okuduğu capability'lerin (Competition/Standings/Prediction/Statistics/Availability/Live) kapsamı — GLOBAL.</summary>
        public int MarketProbabilityEngineReadiness { get; init; }
        /// <summary>AI/haber katmanı (News/Social + motor capability'leri) kapsamı — GLOBAL.</summary>
        public int AiReadiness { get; init; }
        public int LeagueCount { get; init; }
        public int PremiumLeagues { get; init; }
        public int StandardLeagues { get; init; }
        public int LimitedLeagues { get; init; }
        public int PassiveLeagues { get; init; }
        /// <summary>Capability adı → platform-geneli hazırlık (0-100), maç-ağırlıklı — GLOBAL.</summary>
        public Dictionary<string, int> CapabilityReadiness { get; init; } = new();

        // ── OPERATIONAL: FORMAX'ın gerçekten hedeflediği AKTİF pencere (yaklaşan maçı olan ligler), ──
        // ── operasyonel capability'lerde payda = yaklaşan maç. GLOBAL'den bağımsız hesaplanır. ──
        /// <summary>Aktif penceredeki (yaklaşan maçı olan) liglerin operasyonel coverage skoru (0-100), maç-ağırlıklı.</summary>
        public int OperationalCoverage { get; init; }
        /// <summary>Aktif penceredeki motor-capability kapsamı (payda = yaklaşan maç).</summary>
        public int OperationalMotorReadiness { get; init; }
        /// <summary>Aktif penceredeki AI/haber kapsamı.</summary>
        public int OperationalAiReadiness { get; init; }
        /// <summary>Aktif pencerede yaklaşan maçı olan lig sayısı (operasyonel evren).</summary>
        public int OperationalLeagueCount { get; init; }
        /// <summary>Capability adı → operasyonel hazırlık (0-100), yaklaşan-maç-ağırlıklı.</summary>
        public Dictionary<string, int> OperationalCapabilityReadiness { get; init; } = new();
    }

    /// <summary>Trend için hafif zaman-damgalı anlık görüntü (in-memory).</summary>
    public sealed class CoverageSnapshot
    {
        public string TakenUtc { get; init; } = "";
        public int OverallGdpReadiness { get; init; }
        public int MarketProbabilityEngineReadiness { get; init; }
        public int PremiumLeagues { get; init; }
        public int StandardLeagues { get; init; }
    }
}
