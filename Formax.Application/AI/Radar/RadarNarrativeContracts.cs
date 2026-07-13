using System;
using System.Collections.Generic;

namespace Formax.Application.AI.Radar
{
    /// <summary>Hangi yüzey için anlatı üretiliyor.</summary>
    public enum RadarSurface
    {
        /// <summary>Keşfet kartı — kısa özet + highlights + senaryo nedenleri.</summary>
        Discover = 0,

        /// <summary>Maç detayı — tam rapor + section'lar.</summary>
        MatchDetail = 1
    }

    /// <summary>Bir senaryoya (market) eşlik eden LLM açıklaması. Yüzde LLM'den GELMEZ.</summary>
    public sealed class RadarScenarioReason
    {
        public string Market { get; set; } = "";
        public string Reason { get; set; } = "";
    }

    /// <summary>
    /// Pipeline çıktısı — her iki yüzeyin alanlarını taşır. Üretilmeyen alanlar boş kalır.
    /// </summary>
    public sealed class RadarNarrativeResult
    {
        // ── Keşfet ──────────────────────────────────────────────────────────────
        public string RadarSummary { get; set; } = "";
        public List<string> Highlights { get; set; } = new();
        public List<RadarScenarioReason> ScenarioReasons { get; set; } = new();

        // ── Maç Detayı ──────────────────────────────────────────────────────────
        public string MatchReport { get; set; } = "";
        public string WhyThisMatch { get; set; } = "";
        public string ReasoningSummary { get; set; } = "";
        public string NewsSummary { get; set; } = "";
        public string SocialSummary { get; set; } = "";
        public string StatisticalSummary { get; set; } = "";
        public List<string> KeyInsights { get; set; } = new();
        public List<RadarScenarioReason> ScenarioExplanations { get; set; } = new();
        public string EvidenceSummary { get; set; } = "";

        // ── Meta ────────────────────────────────────────────────────────────────
        /// <summary>Reasoning Layer'ın kendi güven skoru (0–100).</summary>
        public int ReasoningConfidence { get; set; }
        /// <summary>true = LLM üretti · false = deterministik fallback.</summary>
        public bool IsAiGenerated { get; set; }
        public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
