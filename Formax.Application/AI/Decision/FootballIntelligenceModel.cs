using System.Collections.Generic;

namespace Formax.Application.AI.Decision
{
    /// <summary>
    /// Football Intelligence v1.0 — MarketProbabilityEngine'in olasılık-DIŞI, oyuncu/kadro odaklı
    /// futbol zekâsı bloğu. TÜM alanlar YALNIZ UnifiedMatchAiContext'ten (GERÇEK GDP sinyalleri) türetilir;
    /// olasılık/gol modeli DEĞİŞMEZ (hash sabit). Her bölüm HasData-gated: veri yoksa BOŞ (uydurma YOK).
    /// LLM bunu yalnız doğal dile çevirir; yeni bilgi/tahmin eklemez.
    ///
    /// 7 katman: Player + Squad + Coach + Transfer (takım-bazlı) + Fixture (takım) + Competition (maç).
    /// Editorial bloğunun DERİN oyuncu-düzeyi tamamlayıcısıdır (Player/Squad artık isimli gerçek veriyle).
    /// </summary>
    public sealed class FootballIntelligence
    {
        public bool HasData { get; init; }
        /// <summary>Kaç takım/bölüm gerçek veriyle doldu (dürüstlük/derinlik göstergesi).</summary>
        public int CoverageDepth { get; init; }

        public TeamFootballIntelligence Home { get; init; } = new();
        public TeamFootballIntelligence Away { get; init; } = new();

        /// <summary>6. Competition Intelligence (lig/kupa/eleme/rövanş/şampiyonluk-küme yarışı) — maç düzeyi.</summary>
        public IReadOnlyList<string> Competition { get; init; } = new List<string>();

        /// <summary>v2 — SENTEZ: birden çok katmanı BİRLEŞTİREN üst-düzey futbol çıkarımları (Player+Timeline+
        /// Standings+Competition çapraz). Tek tek özet değil, ilişkilendirilmiş analiz. Gerçek veri yoksa boş.</summary>
        public IReadOnlyList<string> Synthesis { get; init; } = new List<string>();

        /// <summary>v4 — NEWS INTELLIGENCE: haberin HAM metni DEĞİL, futbola ETKİSİ (sebep→etki implikasyonu).
        /// FORMAX haber göstermez: "kulüp açıkladı/habere göre" gibi kaynak dili YASAK (süzülür). Yalnız gerçek
        /// News/Player/Timeline/Social/Venue sinyallerinden. Story + Editorial buradan beslenir (tek beyin).
        /// Veri yoksa boş (uydurma YOK).</summary>
        public IReadOnlyList<string> NewsImplications { get; init; } = new List<string>();

        /// <summary>vNext — PLAYER NARRATIVE: oyuncu-düzeyi NEDEN→SONUÇ anlatısı (istatistik listesi DEĞİL).
        /// PlayerIntelligence bloğunu TEK okuyan Football üretir (çift-okuma yok); MatchReading.PlayerStory
        /// buradan beslenir. Ör: "Hücum üretimi X üzerinden şekilleniyor; etkisiz kalırsa çeşitlilik azalır."
        /// Gerçek veri yoksa boş (uydurma YOK).</summary>
        public IReadOnlyList<string> PlayerNarrative { get; init; } = new List<string>();
    }

    /// <summary>Tek takımın futbol zekâsı — yalnız gerçek context sinyallerinden.</summary>
    public sealed class TeamFootballIntelligence
    {
        public string TeamName { get; init; } = "";
        public bool HasData { get; init; }

        /// <summary>1. Player Intelligence (en skorer/asist/kilit oyuncu/isimli sakat).</summary>
        public IReadOnlyList<string> Player { get; init; } = new List<string>();
        /// <summary>2. Squad Intelligence (kadro derinliği/pozisyon dağılımı/eksik bölge).</summary>
        public IReadOnlyList<string> Squad { get; init; } = new List<string>();
        /// <summary>3. Coach Intelligence (teknik direktör kimliği).</summary>
        public IReadOnlyList<string> Coach { get; init; } = new List<string>();
        /// <summary>4. Transfer Intelligence (son 12 ay gelen/giden).</summary>
        public IReadOnlyList<string> Transfer { get; init; } = new List<string>();
        /// <summary>5. Fixture Intelligence (dinlenme/yoğunluk/rotasyon) — Timeline'dan.</summary>
        public IReadOnlyList<string> Fixture { get; init; } = new List<string>();
    }
}
