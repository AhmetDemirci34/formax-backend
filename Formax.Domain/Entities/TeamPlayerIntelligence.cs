namespace Formax.Domain.Entities
{
    /// <summary>
    /// FORMAX GDP — Football Intelligence v1.0: takım oyuncu-düzeyi zekâsı (api-football
    /// /players?team=&season= + /injuries?team=&season=). Takım-kapsamlı, düşük frekanslı.
    ///
    /// Identity: TeamProfileSignal ile AYNI desen — kayıt INTERNAL <see cref="TeamId"/> (PK) ile
    /// saklanır, ingestion external id ile provider'ı çağırır, builder doğrudan home/away Team.Id
    /// ile okur. Coverage yoksa satır yazılmaz / HasData=false (fake YOK). Olasılık/gol modeli OKUMAZ
    /// → hash etkilenmez (yalnız Football Intelligence editoryal bloğunu besler).
    /// </summary>
    public class TeamPlayerIntelligence
    {
        /// <summary>Internal Team.Id — PK.</summary>
        public int TeamId { get; set; }

        public string ExternalTeamId { get; set; } = string.Empty;
        public int Season { get; set; }
        public bool HasData { get; set; }

        // ── Kadro / pozisyon dağılımı ──────────────────────────────────────────
        public int SquadPlayerCount { get; set; }
        public int GkCount { get; set; }
        public int DefCount { get; set; }
        public int MidCount { get; set; }
        public int AttCount { get; set; }

        // ── En skorer ──────────────────────────────────────────────────────────
        public string TopScorerName { get; set; } = string.Empty;
        public int TopScorerGoals { get; set; }
        public int TopScorerAssists { get; set; }
        public double TopScorerRating { get; set; }

        // ── Asist lideri ─────────────────────────────────────────────────────────
        public string TopAssistName { get; set; } = string.Empty;
        public int TopAssistCount { get; set; }

        // ── En yüksek ratingli oyuncu (min. süre) ────────────────────────────────
        public string KeyPlayerName { get; set; } = string.Empty;
        public double KeyPlayerRating { get; set; }

        // ── En çok süre alan ─────────────────────────────────────────────────────
        public string MinutesLeaderName { get; set; } = string.Empty;
        public int MinutesLeaderMinutes { get; set; }

        // ── Sakat / cezalı (isimli) ──────────────────────────────────────────────
        public int InjuredCount { get; set; }
        /// <summary>İsimler '|' ile ayrılmış (en fazla ~8).</summary>
        public string InjuredNames { get; set; } = string.Empty;
        public int InjuredDefCount { get; set; }
        public int InjuredMidCount { get; set; }
        public int InjuredAttCount { get; set; }

        // ── v2 Deep Intelligence (gol yükü / bağımlılık / pozisyonel liderler / şut-pas / kart) ──
        public int TeamTotalGoals { get; set; }
        /// <summary>En skorerin takım gollerindeki payı (%).</summary>
        public int TopScorerGoalSharePct { get; set; }
        /// <summary>En skor eden iki oyuncunun toplam payı (%).</summary>
        public int Top2GoalSharePct { get; set; }
        /// <summary>Hücum tek/iki oyuncuya aşırı bağımlı mı (top scorer ≥ %40 veya top2 ≥ %60).</summary>
        public bool OneManDependency { get; set; }

        public string DefenseLeaderName { get; set; } = string.Empty;
        public double DefenseLeaderRating { get; set; }
        public string MidfieldBrainName { get; set; } = string.Empty;
        public int MidfieldBrainAssists { get; set; }
        public double MidfieldBrainRating { get; set; }

        public string ShotsLeaderName { get; set; } = string.Empty;
        public int ShotsLeaderCount { get; set; }
        public string KeyPassLeaderName { get; set; } = string.Empty;
        public int KeyPassLeaderCount { get; set; }

        /// <summary>En çok sarı kart gören (kart/ceza riski).</summary>
        public string CardRiskName { get; set; } = string.Empty;
        public int CardRiskYellows { get; set; }

        public System.DateTime UpdatedAt { get; set; }
    }
}
