using System.Collections.Generic;

namespace Formax.Application.AI.Decision
{
    /// <summary>
    /// v3 Football Intelligence — Match Importance. Maçın önemini TEK skorda toplar; bileşenleri
    /// (Competition/Standings/Derby/Motivation/Tournament/Season/Pressure) şeffaf gösterir. Yalnız
    /// HasData olan bileşenler katkı verir (fake YOK). Deterministik.
    /// </summary>
    public sealed class MatchImportance
    {
        /// <summary>0-100 birleşik önem skoru.</summary>
        public int Score { get; init; }
        /// <summary>DÜŞÜK | ORTA | YÜKSEK | KRİTİK.</summary>
        public string Level { get; init; } = "";
        /// <summary>Katkı veren bileşenler (ad → 0-100 katkı, yalnız gerçek veri).</summary>
        public IReadOnlyList<ImportanceComponent> Components { get; init; } = new List<ImportanceComponent>();
        public string Summary { get; init; } = "";
    }

    public sealed class ImportanceComponent
    {
        public string Name { get; init; } = "";
        public int Contribution { get; init; }   // 0-100
        public string Detail { get; init; } = "";
    }

    // ══════════════════════ v3 FAZ2 — Advanced Intelligence ══════════════════════

    /// <summary>Takım oyun karakteri profili. Yalnız gerçek veriden türetilen boyutlar; gerisi HasData=false.</summary>
    public sealed class TacticalProfile
    {
        public bool HasData { get; init; }
        /// <summary>Hücum-savunma eğilimi (-1 savunmacı .. +1 hücumcu), gol indekslerinden.</summary>
        public double AttackingTilt { get; init; }
        /// <summary>Tempo eğilimi 0-100 (gol beklentisinden).</summary>
        public int TempoLean { get; init; }
        /// <summary>Veri olmayan tarz boyutları (pres/topa sahip olma/kanat/duran top) dürüst listelenir.</summary>
        public IReadOnlyList<string> UnavailableAspects { get; init; } = new List<string>();
        public string Summary { get; init; } = "";
    }

    /// <summary>Psikolojik profil — GDP News/Social/Evidence'ten (gerçek). Coverage yoksa HasData=false.</summary>
    public sealed class PsychologicalProfile
    {
        public bool HasData { get; init; }
        public int PressureScore { get; init; }        // 0-100 (mevcut Pressure)
        public int CoachSituation { get; init; }       // 0-100 TD baskısı
        public int ClubCrisis { get; init; }           // 0-100 kulüp krizi (transfer/açıklama yoğunluğu)
        public int TransferImpact { get; init; }       // 0-100
        public bool OfficialStatements { get; init; }
        public bool BreakingContext { get; init; }
        public string Summary { get; init; } = "";
    }

    /// <summary>Sinyal-arası etkileşim etkisi (birden çok gerçek sinyal birleşince).</summary>
    public sealed class InteractionEffect
    {
        public string Name { get; init; } = "";
        public IReadOnlyList<string> Drivers { get; init; } = new List<string>();
        /// <summary>Etki büyüklüğü 0-100.</summary>
        public int Magnitude { get; init; }
        public string Effect { get; init; } = "";
    }

    /// <summary>Çelişki raporu — yapısal güç vs sentiment/haber. Confidence düşürür.</summary>
    public sealed class ContradictionReport
    {
        public bool HasContradiction { get; init; }
        /// <summary>0-100 çelişki şiddeti.</summary>
        public int Severity { get; init; }
        /// <summary>Confidence'a uygulanan düşüş (puan).</summary>
        public int ConfidencePenalty { get; init; }
        public IReadOnlyList<string> Details { get; init; } = new List<string>();
        public string Summary { get; init; } = "";
    }

    /// <summary>Sürpriz/upset uyarısı — favori olmasına rağmen yüksek risk.</summary>
    public sealed class SurpriseAlert
    {
        public bool HasAlert { get; init; }
        /// <summary>0-100 sürpriz potansiyeli.</summary>
        public int Potential { get; init; }
        public string FavoredSide { get; init; } = "";    // Home | Away | None
        public string Summary { get; init; } = "";
    }

    // ══════════════════════ v3 FAZ3 — Live Intelligence ══════════════════════

    /// <summary>Canlı momentum — mevcut MatchLiveStats/Momentum'dan (gerçek). In-play değilse HasData=false.</summary>
    public sealed class LiveMomentum
    {
        public bool HasData { get; init; }
        public int Minute { get; init; }
        public int HomeScore { get; init; }
        public int AwayScore { get; init; }
        /// <summary>Anlık momentum (-100 dep .. +100 ev), canlı istatistiklerden.</summary>
        public int Momentum { get; init; }
        /// <summary>Momentum'u besleyen gerçek istatistikler (yoksa listelenmez).</summary>
        public IReadOnlyList<string> Drivers { get; init; } = new List<string>();
        public string Summary { get; init; } = "";
    }
}
