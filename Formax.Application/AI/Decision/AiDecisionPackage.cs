using System.Collections.Generic;

namespace Formax.Application.AI.Decision
{
    /// <summary>
    /// FORMAX'ın BEYNİ — MarketProbabilityEngine'in TEK çıktısı.
    ///
    /// Bu paket tüm AI ekranlarının (Keşfet, Maç Detayı, Canlı, Bildirim, Radar, Global) ortak
    /// kaynağıdır. Farklı ekranlar farklı AI üretmez; hepsi bu paketi okur. LLM yalnız bu paketi
    /// kullanıcı diline çevirir (karar vermez).
    ///
    /// Deterministik: aynı <see cref="Context.UnifiedMatchAiContext"/> → aynı AiDecisionPackage.
    /// Motor bu paketi YALNIZ Unified AI Context'ten türetir (repo/provider/ham veri YOK).
    /// </summary>
    public sealed class AiDecisionPackage
    {
        public int MatchId { get; init; }
        public string HomeName { get; init; } = "Ev sahibi";
        public string AwayName { get; init; } = "Deplasman";

        /// <summary>Paket şema sürümü (versionlanabilir; yeni sinyal eklense de tüketici kırılmaz).</summary>
        public string Version { get; init; } = "ai-decision-package/v1";

        /// <summary>Maçın karakteri (12 boyut). AI yorumunun temel verisi.</summary>
        public MatchDna Dna { get; init; } = new();

        /// <summary>AI olasılıkları — en az 13 market, her biri Probability + Confidence + Reason.</summary>
        public IReadOnlyList<AiProbability> Probabilities { get; init; } = new List<AiProbability>();

        /// <summary>En güçlü 3 senaryo (çelişki/tekrar elenmiş), her biri Risk taşır.</summary>
        public IReadOnlyList<AiScenario> Scenarios { get; init; } = new List<AiScenario>();

        /// <summary>Alternatif (top-3 dışı) dikkat çeken senaryolar — açıklanabilirlik için.</summary>
        public IReadOnlyList<AiScenario> AlternativeScenarios { get; init; } = new List<AiScenario>();

        /// <summary>Canlı AI projeksiyonları (xG-türevli; canlı sinyal geldikçe yeniden hesaplanır).</summary>
        public LiveProjection Live { get; init; } = new();

        /// <summary>Kararın genel güveni (Unified Context kalitesi + sinyal uzlaşısından).</summary>
        public DecisionConfidence Confidence { get; init; } = new();

        /// <summary>Motorun tespit ettiği riskler (kaos, çelişki, düşük veri, kadro, sürpriz...).</summary>
        public IReadOnlyList<AiRisk> Risks { get; init; } = new List<AiRisk>();

        /// <summary>Açıklanabilirlik: en etkili sinyaller, gerekçe zinciri, alternatifler.</summary>
        public DecisionExplainability Explainability { get; init; } = new();

        /// <summary>
        /// v2 Evolution — MAÇIN BAĞLAMI (Standings/Motivation/Derby/Pressure). Additif; mevcut
        /// kontratı bozmaz. Veri yoksa alt bloklar HasData=false (motor v1 gibi davranır).
        /// </summary>
        public ContextIntelligence Context { get; init; } = new();

        // ── v3 Football + Advanced + Live Intelligence (hepsi additive) ──
        /// <summary>v3 — Maçın önemi (tek skor + bileşen dökümü).</summary>
        public MatchImportance Importance { get; init; } = new();
        /// <summary>v3 — Takım oyun karakteri (gerçek veriden; gerisi HasData=false).</summary>
        public TacticalProfile Tactical { get; init; } = new();
        /// <summary>v3 — Psikolojik profil (News/Social/Evidence).</summary>
        public PsychologicalProfile Psychology { get; init; } = new();
        /// <summary>v3 — Sinyal-arası etkileşim etkileri.</summary>
        public IReadOnlyList<InteractionEffect> Interactions { get; init; } = new List<InteractionEffect>();
        /// <summary>v3 — Çelişki raporu (yapı vs sentiment → confidence).</summary>
        public ContradictionReport Contradiction { get; init; } = new();
        /// <summary>v3 — Sürpriz/upset uyarısı.</summary>
        public SurpriseAlert Surprise { get; init; } = new();
        /// <summary>v3 — Canlı momentum (in-play değilse HasData=false).</summary>
        public LiveMomentum LiveMomentum { get; init; } = new();
        /// <summary>v3 — Gizli sinyaller: düşük ağırlıklı ama dikkate değer aktif sinyaller.</summary>
        public IReadOnlyList<string> HiddenSignals { get; init; } = new List<string>();

        /// <summary>Kararı en çok etkileyen ağırlıklı sinyaller (şeffaflık).</summary>
        public IReadOnlyList<TopSignal> TopSignals { get; init; } = new List<TopSignal>();

        // ── v1.1 — DECISION PACKAGE v2 ANATOMİSİ (additive; olasılıkları değiştirmez, deterministik) ──
        /// <summary>Ana senaryo — en ayırt edici/olası senaryo.</summary>
        public AiScenario PrimaryScenario { get; init; } = new();
        /// <summary>Alternatif senaryo — ana senaryoya en güçlü rakip.</summary>
        public AiScenario AlternativeScenario { get; init; } = new();
        /// <summary>Sürpriz senaryo — düşük olasılıklı ama upset/çelişki nedeniyle dikkate değer.</summary>
        public AiScenario SurpriseScenario { get; init; } = new();
        /// <summary>Kritik faktörler — kararı belirleyen en önemli bağlam/DNA/önem unsurları.</summary>
        public IReadOnlyList<string> CriticalFactors { get; init; } = new List<string>();
        /// <summary>Karar sürücüleri — en ağırlıklı yönlü sinyaller (insan-okur).</summary>
        public IReadOnlyList<string> DecisionDrivers { get; init; } = new List<string>();
        /// <summary>Bilinmeyen faktörler — verisi olmayan (HasData=false) önemli bloklar (dürüstlük).</summary>
        public IReadOnlyList<string> UnknownFactors { get; init; } = new List<string>();
        /// <summary>Maçın karakteri (personality) — DNA özeti + oyun yönelimi.</summary>
        public MatchPersonality Personality { get; init; } = new();
        /// <summary>İç tutarlılık raporu (olasılık↔DNA↔güven çapraz denetimi + otomatik düzeltme).</summary>
        public DecisionConsistency Consistency { get; init; } = new();
        /// <summary>0-100 Decision Quality Score (reasoning/confidence/evidence/consistency/coverage) — YALNIZ engine-içi kalite metriği.</summary>
        public int DecisionQualityScore { get; init; }

        /// <summary>v4 — EDITORIAL INTELLIGENCE: olasılık-dışı, gerçek-veriye-dayalı editoryal analiz (12 bölüm).
        /// LLM YALNIZ bunu doğal Türkçeye çevirir; yeni analiz üretmez. Veri yoksa bölümler boş (uydurma YOK).</summary>
        public EditorialIntelligence Editorial { get; init; } = new();

        /// <summary>Football Intelligence v1.0 — oyuncu/kadro odaklı 7 katmanlı futbol zekâsı (Player/Squad/
        /// Coach/Transfer/Fixture/Competition). GERÇEK api-football verisinden; olasılığa dokunmaz (hash sabit).
        /// Editorial'ın DERİN oyuncu-düzeyi tamamlayıcısı. Veri yoksa bölümler boş (uydurma YOK).</summary>
        public FootballIntelligence FootballIntelligence { get; init; } = new();

        /// <summary>Football AI Brain v3 — STORY ENGINE: tüm Intelligence katmanlarını tek hikâyeye indiren
        /// editoryal anlatı (ana/yan hikâye, kırılma, avantaj, risk, sürpriz, FORMAX görüşü, haber implikasyonları).
        /// Mevcut bileşenlerden türetilir; olasılığa dokunmaz (hash sabit). LLM yalnız bunu okur.</summary>
        public MatchStory Story { get; init; } = new();

        /// <summary>AI Brain vNext — MATCH READING: tek anlatı beyninin sıralı editoryal okuması (bağlam→
        /// takımlar→kadro→oyuncular→haber→taktik→psikoloji→kırılma→FORMAX görüşü + canlı okuma). Editorial+
        /// Story bunun projeksiyonudur (tek okuma/tek gerçek). LLM bunu doğal Türkçeye çevirir; olasılığa
        /// dokunmaz (hash sabit). Veri yoksa bölümler boş (uydurma YOK).</summary>
        public MatchReading Reading { get; init; } = new();

        /// <summary>Paket meta verisi: veri kalitesi, sinyal sayıları, çelişki özeti, determinizm imzası.</summary>
        public DecisionMeta Meta { get; init; } = new();
    }

    // ══════════════════════════════ MATCH DNA ══════════════════════════════

    /// <summary>
    /// Maçın karakteri — 12 boyut. Her boyut 0-100 skor + güven + gerçek gerekçe taşır.
    /// Gerçek sinyalle desteklenen boyut yüksek güvenli; yalnız proxy'den türetilen boyut
    /// dürüstçe düşük güvenli işaretlenir (fake YOK; türetim gücü şeffaftır).
    /// </summary>
    public sealed class MatchDna
    {
        public DnaDimension Tempo { get; init; } = new();               // Tempo
        public DnaDimension GoalPotential { get; init; } = new();       // Gol Potansiyeli
        public DnaDimension Balance { get; init; } = new();             // Maç Dengesi
        public DnaDimension Pressure { get; init; } = new();            // Baskı
        public DnaDimension PhysicalIntensity { get; init; } = new();   // Fiziksel Sertlik
        public DnaDimension CounterAttack { get; init; } = new();       // Kontratak Eğilimi
        public DnaDimension SetPieceThreat { get; init; } = new();      // Duran Top Etkisi
        public DnaDimension EarlyGoalTendency { get; init; } = new();   // Erken Gol Eğilimi
        public DnaDimension LateGoalTendency { get; init; } = new();    // Geç Gol Eğilimi
        public DnaDimension ChaosRisk { get; init; } = new();           // Kaos Riski
        public DnaDimension SurprisePotential { get; init; } = new();   // Sürpriz Potansiyeli
        public DnaDimension Openness { get; init; } = new();            // Maçın Açılma Eğilimi

        /// <summary>v3 — DNA boyutlarından türetilen maç arketipleri (Açık/Kontrollü/Tempolu/Sabırlı/Fiziksel/Kontratak/Kaotik/Dengeli...). Builder yazar (post-profile).</summary>
        public IReadOnlyList<string> Archetypes { get; set; } = new List<string>();

        /// <summary>Tüm boyutları tek listede döndürür (LLM/UI iterasyonu için).</summary>
        public IReadOnlyList<DnaDimension> All() => new List<DnaDimension>
        {
            Tempo, GoalPotential, Balance, Pressure, PhysicalIntensity, CounterAttack,
            SetPieceThreat, EarlyGoalTendency, LateGoalTendency, ChaosRisk, SurprisePotential, Openness
        };
    }

    /// <summary>Tek DNA boyutu — skor + güven + türetim gerekçesi.</summary>
    public sealed class DnaDimension
    {
        public string Name { get; init; } = "";
        /// <summary>0-100 boyut şiddeti.</summary>
        public int Score { get; init; }
        /// <summary>0-100 bu boyutun türetim güveni (gerçek sinyal → yüksek, proxy → düşük).</summary>
        public int Confidence { get; init; }
        /// <summary>Kısa insan-okur etiket (ör. "Yüksek tempo").</summary>
        public string Label { get; init; } = "";
        /// <summary>Türetim gerekçesi (hangi sinyal/blok).</summary>
        public string Basis { get; init; } = "";
    }

    // ══════════════════════════════ OLASILIKLAR ══════════════════════════════

    /// <summary>Bir AI olasılığı — market, olasılık, güven, gerekçe. Deterministik (Poisson türevi).</summary>
    public sealed class AiProbability
    {
        public string Market { get; init; } = "";
        /// <summary>0-100 olasılık.</summary>
        public int Probability { get; init; }
        /// <summary>YÜKSEK | ORTA | DÜŞÜK.</summary>
        public string Confidence { get; init; } = "";
        /// <summary>Neden bu olasılık — DNA/sinyal temelli kısa gerekçe.</summary>
        public string Reason { get; init; } = "";
        /// <summary>Market ailesi (Outcome/Totals/Btts/Half/TeamGoals/Score...).</summary>
        public string Family { get; init; } = "";

        /// <summary>
        /// GERÇEK market oranı (sağlayıcı verisi, MatchMarketOdds). Motor bunu ÜRETMEZ —
        /// deterministik paket kurulduktan sonra use-case katmanında enjekte edilir. Sağlayıcıda
        /// karşılığı olmayan marketlerde null kalır ve UI oran göstermez (uydurulmaz).
        /// </summary>
        public decimal? Odd { get; set; }

        /// <summary>Bir önceki gerçek oran (hareket yönü). Yoksa null.</summary>
        public decimal? PreviousOdd { get; set; }
    }

    // ══════════════════════════════ SENARYO ══════════════════════════════

    /// <summary>Top senaryo — olasılık + güven + gerekçe + risk.</summary>
    public sealed class AiScenario
    {
        public string Title { get; init; } = "";
        public int Probability { get; init; }
        public string Confidence { get; init; } = "";
        public string Reason { get; init; } = "";
        /// <summary>Bu senaryonun taşıdığı risk (kısa).</summary>
        public string Risk { get; init; } = "";
        public string Family { get; init; } = "";
    }

    // ══════════════════════════════ CANLI ══════════════════════════════

    /// <summary>
    /// Canlı AI projeksiyonu. Şu an context'te canlı blok yoksa <see cref="IsLive"/>=false
    /// ve değerler MAÇ ÖNCESİ beklenen-gol modelinden (gerçek xG) türetilir. Canlı sinyaller
    /// context'e geldikçe aynı motor yeniden hesaplar (fake YOK — deterministik model çıktısı).
    /// </summary>
    public sealed class LiveProjection
    {
        public bool IsLive { get; init; }
        /// <summary>Projeksiyon temeli açıklaması (maç öncesi / canlı).</summary>
        public string Basis { get; init; } = "";
        public IReadOnlyList<LiveOutcome> Outcomes { get; init; } = new List<LiveOutcome>();
    }

    /// <summary>Tek canlı projeksiyon çıktısı.</summary>
    public sealed class LiveOutcome
    {
        public string Market { get; init; } = "";
        public int Probability { get; init; }
        public string Confidence { get; init; } = "";
        public string Reason { get; init; } = "";
    }

    // ══════════════════════════════ GÜVEN / RİSK ══════════════════════════════

    /// <summary>Kararın genel güveni.</summary>
    public sealed class DecisionConfidence
    {
        /// <summary>0-100 genel güven.</summary>
        public int Score { get; init; }
        /// <summary>YÜKSEK | ORTA | DÜŞÜK.</summary>
        public string Level { get; init; } = "";
        /// <summary>Güvenin dayanağı (aktif sinyal, veri kalitesi, uzlaşı).</summary>
        public string Basis { get; init; } = "";
    }

    /// <summary>Motorun tespit ettiği bir risk.</summary>
    public sealed class AiRisk
    {
        public string Type { get; init; } = "";
        /// <summary>0-100 risk şiddeti.</summary>
        public int Severity { get; init; }
        /// <summary>DÜŞÜK | ORTA | YÜKSEK.</summary>
        public string Level { get; init; } = "";
        public string Description { get; init; } = "";
    }

    // ══════════════════════════════ AÇIKLANABİLİRLİK ══════════════════════════════

    /// <summary>Kararın açıklanabilirlik paketi.</summary>
    public sealed class DecisionExplainability
    {
        /// <summary>Motorun bu maçı nasıl gördüğü — kısa özet cümleler.</summary>
        public IReadOnlyList<string> Reasoning { get; init; } = new List<string>();
        /// <summary>En etkili sinyal adları (özet).</summary>
        public IReadOnlyList<string> KeyDrivers { get; init; } = new List<string>();
        /// <summary>Dikkat edilmesi gereken riskler (özet).</summary>
        public IReadOnlyList<string> Cautions { get; init; } = new List<string>();

        // ── v1.1 — açıklanabilirlik derinleştirme (yalnız gerçek sinyalden; uydurma YOK) ──
        /// <summary>Kararı GÜÇLENDİREN etkenler (net yönle hizalı aktif sinyaller).</summary>
        public IReadOnlyList<string> StrengtheningFactors { get; init; } = new List<string>();
        /// <summary>Kararı ZAYIFLATAN etkenler (çelişki, düşük veri, yüksek risk).</summary>
        public IReadOnlyList<string> WeakeningFactors { get; init; } = new List<string>();
        /// <summary>Kararı değiştirebilecek TEK kritik olay (en güçlü ters/belirsizlik kaynağı).</summary>
        public string PivotalFactor { get; init; } = "";
    }

    // ══════════════════════════════ v1.1 KALİTE / TUTARLILIK / KARAKTER ══════════════════════════════

    /// <summary>
    /// Maçın karakteri (personality) — DNA boyutlarından + oyun yöneliminden türetilir. Yalnız etiket
    /// değil: Decision Package'in reasoning/risk/senaryo katmanlarını besleyen DNA'nın özetidir.
    /// </summary>
    public sealed class MatchPersonality
    {
        /// <summary>Baskın karakter (ör. "Kaotik", "Kontrollü", "Tempolu", "Dengeli").</summary>
        public string Primary { get; init; } = "";
        /// <summary>Tüm karakter özellikleri (DNA arketipleri).</summary>
        public IReadOnlyList<string> Traits { get; init; } = new List<string>();
        /// <summary>Oyun yönelimi: "Hücum ağırlıklı" | "Savunma ağırlıklı" | "Dengeli".</summary>
        public string PlayStyle { get; init; } = "";
        public string Summary { get; init; } = "";
    }

    /// <summary>
    /// İç tutarlılık raporu — motorun kendi çıktısını (olasılık ↔ DNA ↔ güven ↔ çelişki) çapraz
    /// denetlemesi. Tutarsızlık bulunursa ilgili market güveni düşürülür (olasılık DEĞİŞMEZ; deterministik).
    /// </summary>
    public sealed class DecisionConsistency
    {
        /// <summary>0-100 iç tutarlılık skoru (100 = çelişki yok).</summary>
        public int Score { get; init; } = 100;
        public bool IsConsistent { get; init; } = true;
        /// <summary>Tespit edilen iç çelişkiler.</summary>
        public IReadOnlyList<string> Issues { get; init; } = new List<string>();
        /// <summary>Uygulanan otomatik düzeltmeler (güven düşürme vb.).</summary>
        public IReadOnlyList<string> Corrections { get; init; } = new List<string>();
        /// <summary>Tutarsızlık nedeniyle güveni düşürülen marketler.</summary>
        public IReadOnlyList<string> FlaggedMarkets { get; init; } = new List<string>();
    }

    /// <summary>Kararı etkileyen tek ağırlıklı sinyal (şeffaflık kaydı).</summary>
    public sealed class TopSignal
    {
        public string Name { get; init; } = "";
        public string Category { get; init; } = "";
        /// <summary>0-100 normalize edilmiş etki ağırlığı (dinamik).</summary>
        public int Weight { get; init; }
        /// <summary>Yönlü etki (-1..+1): + ev, - deplasman, 0 yönsüz.</summary>
        public double Direction { get; init; }
        public int Confidence { get; init; }
        public string Reason { get; init; } = "";
    }

    // ══════════════════════════════ META ══════════════════════════════

    /// <summary>Paket meta verisi — kalite + determinizm imzası.</summary>
    public sealed class DecisionMeta
    {
        public string ContextVersion { get; init; } = "";
        public int TotalSignalCount { get; init; }
        public int ActiveSignalCount { get; init; }
        public double OverallDataQuality { get; init; }
        public Dictionary<string, int> ConflictSummary { get; init; } = new();
        /// <summary>Beklenen goller (model şeffaflığı).</summary>
        public double ExpectedGoalsHome { get; init; }
        public double ExpectedGoalsAway { get; init; }
        /// <summary>Net yön eğilimi (-1..+1): + ev lehine, ağırlıklı sinyal alanından.</summary>
        public double NetHomeEdge { get; init; }
        /// <summary>Determinizm imzası: aynı context aynı hash → aynı paket (doğrulanabilir).</summary>
        public string DeterminismHash { get; init; } = "";
    }
}
