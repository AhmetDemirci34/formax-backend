using System.Collections.Generic;

namespace Formax.Application.AI.Decision
{
    /// <summary>
    /// v2 Evolution — MAÇIN BAĞLAMI. MarketProbabilityEngine'in yalnız olasılık değil, maçın
    /// önemini/psikolojisini/motivasyonunu da değerlendirmesini sağlayan yorum katmanı.
    ///
    /// Tüm alanlar GERÇEK Unified AI Context bloklarından (Standings/News/Social) türetilir; ham veri
    /// veya GDP/AiSignalFactory'ye dokunulmaz. Veri yoksa HasData=false (fake YOK). Bu blok AI Decision
    /// Package'a ADDİTİF eklenir (mevcut kontrat bozulmaz).
    /// </summary>
    public sealed class ContextIntelligence
    {
        public StandingsInsight Standings { get; init; } = new();
        public MotivationInsight Motivation { get; init; } = new();
        public DerbyInsight Derby { get; init; } = new();
        public PressureInsight Pressure { get; init; } = new();

        // v2.5 — READ-ONLY bağlam bloklarından türetilen yorumlar (additive).
        public CompetitionInsight Competition { get; init; } = new();
        public TournamentInsight Tournament { get; init; } = new();
        public SeasonInsight Season { get; init; } = new();

        /// <summary>Bağlam katmanının açıklanabilir özet cümleleri (yalnız HasData olanlardan).</summary>
        public IReadOnlyList<string> Explanations { get; init; } = new List<string>();
    }

    /// <summary>Maçın önemi/türü yorumu — Competition context bloğundan (canonical). Boşsa HasData=false.</summary>
    public sealed class CompetitionInsight
    {
        public bool HasData { get; init; }
        public string CompetitionType { get; init; } = ""; // League | Cup | Knockout
        public string Stage { get; init; } = "";
        public int Round { get; init; }
        public string Importance { get; init; } = "";      // Normal | High | Critical | Elimination
        public bool IsElimination { get; init; }
        /// <summary>Bağlamın stake çarpanı (0..1): Normal 0, High 0.5, Critical 0.8, Elimination 1.0.</summary>
        public double StakeLevel { get; init; }
        public string Summary { get; init; } = "";
    }

    /// <summary>Turnuva/eleme yorumu — Tournament context bloğundan. Boşsa HasData=false.</summary>
    public sealed class TournamentInsight
    {
        public bool HasData { get; init; }
        public bool ExtraTimePossible { get; init; }
        public bool PenaltiesPossible { get; init; }
        public bool AggregateMatters { get; init; }
        public string Summary { get; init; } = "";
    }

    /// <summary>Sezon bağlamı yorumu — Season context bloğundan (Match.MatchDate'ten, gerçek).</summary>
    public sealed class SeasonInsight
    {
        public bool HasData { get; init; }
        public int SeasonYear { get; init; }
        public string SeasonPhase { get; init; } = "";     // Start | Middle | End
        /// <summary>Sezon-sonu stake çarpanı (0..1): End'e yaklaştıkça artar.</summary>
        public double EndStake { get; init; }
        public string Summary { get; init; } = "";
    }

    /// <summary>Puan durumu bağlamı (göreli). Zone/şampiyonluk% lig büyüklüğü gerektirir → üretilmez (dürüst).</summary>
    public sealed class StandingsInsight
    {
        public bool HasData { get; init; }
        public int HomePosition { get; init; }
        public int AwayPosition { get; init; }
        public int HomePoints { get; init; }
        public int AwayPoints { get; init; }
        public int PointGap { get; init; }          // ev − dep
        public int GoalDiffGap { get; init; }        // ev − dep averaj farkı
        public string HigherPlacedSide { get; init; } = ""; // "Home" | "Away" | "Level"
        /// <summary>Sıralama bağlamı yön eğilimi (-1..+1, + ev). Motivasyon/açıklama için; edge'e ENJEKTE EDİLMEZ (çift sayım önlenir).</summary>
        public double Lean { get; init; }
        public int Confidence { get; init; }
        public string Summary { get; init; } = "";

        // v2.5 — tam tablo bağlamı (StandingsContext bloğundan; kısmi kapsamda -1/false).
        public int LeaderPoints { get; init; }
        public string LeaderName { get; init; } = "";
        public int HomeGapToLeader { get; init; } = -1;
        public int AwayGapToLeader { get; init; } = -1;
        public bool TitleRace { get; init; }
        public bool RelegationDataAvailable { get; init; }
        // v3 — bölge sınıflandırması (yalnız tam tablo mevcutsa; aksi halde "Kısmi kapsam").
        public string HomeZone { get; init; } = "";
        public string AwayZone { get; init; } = "";
    }

    /// <summary>Motivasyon bağlamı (göreli standings + form türevi). Zone/sezon verisi olmadan kısmi (dürüst).</summary>
    public sealed class MotivationInsight
    {
        public bool HasData { get; init; }
        public int HomeMotivation { get; init; }     // 0-100
        public int AwayMotivation { get; init; }     // 0-100
        public double Lean { get; init; }            // -1..+1 (+ ev)
        public string HomeLabel { get; init; } = "";
        public string AwayLabel { get; init; } = "";
        public string Summary { get; init; } = "";
    }

    /// <summary>Derbi bağlamı — YALNIZ GERÇEK News "Derby" sinyali varsa (hardcode YOK).</summary>
    public sealed class DerbyInsight
    {
        public bool HasData { get; init; }
        public double Intensity { get; init; }       // 0..1
        public string Source { get; init; } = "";    // "News"
        public string Summary { get; init; } = "";
    }

    /// <summary>Baskı bağlamı — News/Social'dan (TD/kulüp/resmi açıklama/breaking). Maç düzeyi (takım ayrımı yok → dürüst).</summary>
    public sealed class PressureInsight
    {
        public bool HasData { get; init; }
        public int OverallPressure { get; init; }    // 0-100
        public string Scope { get; init; } = "";     // "Match-level"
        public string Summary { get; init; } = "";
    }
}
