using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Formax.Application.AI.Radar
{
    /// <summary>
    /// FORMAX Radar v2 — LLM'in gördüğü TEK, SİNDİRİLMİŞ bağlam.
    ///
    /// Tasarım kuralı: Buraya ham haber gövdesi, ham istatistik tablosu veya ham
    /// sosyal veri KONULMAZ. Yalnız önceden hesaplanmış sinyaller, oranlar ve kısa
    /// tema başlıkları bulunur. LLM bunları "tekrar etmez", "anlamlandırır".
    ///
    /// Tüm yüzdeler/skorlar deterministik motorlardan gelir — LLM hesaplamaz.
    /// </summary>
    public sealed class MatchIntelligenceContext
    {
        public int MatchId { get; set; }
        public string HomeTeam { get; set; } = "";
        public string AwayTeam { get; set; } = "";
        public string League { get; set; } = "";
        public string? Round { get; set; }
        public string Status { get; set; } = "";
        public string KickoffUtc { get; set; } = "";

        public ImportanceBlock Importance { get; set; } = new();
        public FormBlock Form { get; set; } = new();
        public StatsBlock Stats { get; set; } = new();
        public H2HBlock H2H { get; set; } = new();
        public NewsBlock News { get; set; } = new();
        public SocialBlock Social { get; set; } = new();

        /// <summary>Deterministik motorun ürettiği, sıralı top senaryolar. LLM dokunmaz.</summary>
        public List<ScenarioBlock> Scenarios { get; set; } = new();

        /// <summary>WorldPerceptionProvider üst-bağlamı (günün futbol havası).</summary>
        public string? WorldHeadline { get; set; }

        public sealed class ImportanceBlock
        {
            public string Level { get; set; } = "";       // sapma bölgesi / önem etiketi
            public int WatchersCount { get; set; }
            public string? Note { get; set; }              // kısa sapma metni
        }

        public sealed class FormBlock
        {
            public string HomeRecent { get; set; } = "";   // ör. "G G B M G" (son 5)
            public string AwayRecent { get; set; } = "";
            public int HomeFormScore { get; set; }
            public int AwayFormScore { get; set; }
        }

        public sealed class StatsBlock
        {
            public double HomeAvgGoalsFor { get; set; }
            public double AwayAvgGoalsFor { get; set; }
            public int HomeGoalScoringRate { get; set; }
            public int AwayGoalScoringRate { get; set; }
            public int HomeCleanSheetRate { get; set; }
            public int AwayCleanSheetRate { get; set; }
            public int? HomeRank { get; set; }
            public int? AwayRank { get; set; }
        }

        public sealed class H2HBlock
        {
            public int Total { get; set; }
            public int HomeWins { get; set; }
            public int AwayWins { get; set; }
            public int Draws { get; set; }
        }

        public sealed class NewsBlock
        {
            public int Volume24h { get; set; }
            public string? TopType { get; set; }           // baskın sinyal/tip (Transfer/Injury...)
            public List<string> Themes { get; set; } = new(); // kısa başlık temaları (max 3)
            /// <summary>v2.1 Evidence Store sinyalleri (Transfer, Injury, Derby...). Boşsa eski kaynak.</summary>
            public List<string> Signals { get; set; } = new();
            /// <summary>Evidence kaynağı kullanıldı mı (Reasoning bunu bilir).</summary>
            public bool FromEvidence { get; set; }
        }

        // Topluluk/taraftar ilgisi — iç sinyal proxy'si. Dış sosyal (X/Reddit) henüz yok.
        public sealed class SocialBlock
        {
            public int CommunityInterest { get; set; }   // takip eden kullanıcı sayısı
            public string Level { get; set; } = "";       // Düşük / Orta / Yüksek
        }

        public sealed class ScenarioBlock
        {
            public string Market { get; set; } = "";
            public int Probability { get; set; }
            public string Confidence { get; set; } = "";
            /// <summary>Bu senaryoyu destekleyen deterministik kanıt etiketleri (LLM nedeni buna dayandırır).</summary>
            public List<string> EvidenceTags { get; set; } = new();
        }

        private static readonly JsonSerializerOptions PromptJson = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

        /// <summary>Prompt'a gömülecek kompakt JSON gösterimi.</summary>
        public string ToPromptJson() => JsonSerializer.Serialize(this, PromptJson);
    }
}
