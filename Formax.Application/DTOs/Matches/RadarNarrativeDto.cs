using System.Collections.Generic;

namespace Formax.Application.DTOs.Matches;

/// <summary>
/// FORMAX Radar v2 — LLM anlatı çıktısı (frontend-facing).
/// MatchDetailDto'ya opsiyonel olarak eklenir; gelmezse UI mevcut davranışına döner.
/// Hem Keşfet (Summary/Highlights) hem Maç Detayı (Report/sections) alanlarını taşır.
/// </summary>
public sealed class RadarNarrativeDto
{
    // ── Keşfet ───────────────────────────────────────────────────────────────
    public string RadarSummary { get; set; } = "";
    public List<string> Highlights { get; set; } = new();

    // ── Maç Detayı ───────────────────────────────────────────────────────────
    public string MatchReport { get; set; } = "";
    public string WhyThisMatch { get; set; } = "";
    public string ReasoningSummary { get; set; } = "";
    public string NewsSummary { get; set; } = "";
    public string SocialSummary { get; set; } = "";
    public string StatisticalSummary { get; set; } = "";
    public List<string> KeyInsights { get; set; } = new();

    /// <summary>Senaryo açıklamaları (market + neden). Yüzdeler Probabilities'ten gelir.</summary>
    public List<RadarScenarioReasonDto> Scenarios { get; set; } = new();

    public string EvidenceSummary { get; set; } = "";

    // ── AI İncele ────────────────────────────────────────────────────────────
    /// <summary>Maç listesinden hızlı okunan orta boy AI özeti.</summary>
    public string AiIncele { get; set; } = "";

    /// <summary>Reasoning Layer'ın kendi güven skoru (0–100).</summary>
    public int ReasoningConfidence { get; set; }

    /// <summary>true = LLM üretti · false = deterministik fallback.</summary>
    public bool IsAiGenerated { get; set; }
}

public sealed class RadarScenarioReasonDto
{
    public string Market { get; set; } = "";
    public string Reason { get; set; } = "";
}
