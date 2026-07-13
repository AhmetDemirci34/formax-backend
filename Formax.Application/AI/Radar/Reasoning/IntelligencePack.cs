using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Formax.Application.AI.Radar.Reasoning
{
    /// <summary>
    /// FORMAX Radar v3 — Reasoning Layer çıktısı. LLM'e artık ham context değil, BU
    /// gönderilir. ReasoningEngine ham veriden anlam çıkarır; pack yalnızca SİNDİRİLMİŞ
    /// akıl yürütme taşır: güçlü sinyaller, çelişkiler, anlatı odağı, kanıt paketi,
    /// reasoning güven skoru ve sıralı senaryolar.
    ///
    /// LLM bu pack'i OKUR ve ANLATIR; düşünmeyi ReasoningEngine yapmıştır.
    /// </summary>
    public sealed class IntelligencePack
    {
        public int MatchId { get; set; }
        public string HomeTeam { get; set; } = "";
        public string AwayTeam { get; set; } = "";
        public string League { get; set; } = "";
        public string KickoffUtc { get; set; } = "";

        /// <summary>Reasoning Layer'ın kendi güven skoru (0–100).</summary>
        public int ReasoningConfidence { get; set; }

        /// <summary>Ham veriden çıkarılan güçlü sinyaller.</summary>
        public List<ReasonedSignal> Signals { get; set; } = new();

        /// <summary>Tespit edilen çelişkiler — LLM bunları açıkça dengelemeli.</summary>
        public List<string> Contradictions { get; set; } = new();

        /// <summary>LLM hangi konulara, hangi öncelikle odaklanmalı (sıralı).</summary>
        public List<string> NarrativeFocus { get; set; } = new();

        /// <summary>Anlamlı, sindirilmiş kanıtlar (ham veri değil).</summary>
        public List<EvidenceItem> Evidence { get; set; } = new();

        /// <summary>Deterministik sıralı senaryolar (LLM yüzdeye dokunmaz).</summary>
        public List<ScenarioInsight> Scenarios { get; set; } = new();

        public string? WorldHeadline { get; set; }

        public sealed class ReasonedSignal
        {
            public string Name { get; set; } = "";
            public int Strength { get; set; }    // 0–100
            public int Confidence { get; set; }  // 0–100
            public string Evidence { get; set; } = "";
        }

        public sealed class EvidenceItem
        {
            public string Label { get; set; } = "";
            public string Value { get; set; } = "";
        }

        public sealed class ScenarioInsight
        {
            public string Market { get; set; } = "";
            public int Probability { get; set; }
            public string Confidence { get; set; } = "";
            public string Reason { get; set; } = "";
        }

        private static readonly JsonSerializerOptions PromptJson = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

        public string ToPromptJson() => JsonSerializer.Serialize(this, PromptJson);
    }
}
