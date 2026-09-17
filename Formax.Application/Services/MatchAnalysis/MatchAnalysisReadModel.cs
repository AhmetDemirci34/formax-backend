using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Formax.Application.Services.MatchAnalysis
{
    /// <summary>
    /// Kullanıcıya giden AI Maç Analizi — yalnız arka planda üretilmiş ve doğrulanmış kayıttan.
    /// Bölümler boşsa arayüz o bölümü göstermez; kayıt yoksa <see cref="Status"/> "Preparing".
    /// </summary>
    public sealed class MatchAnalysisDto
    {
        /// <summary>"Ready" | "Preparing" | "InsufficientData".</summary>
        public string Status { get; set; } = "Preparing";
        public DateTime? GeneratedAtUtc { get; set; }

        /// <summary>BU MAÇI NEDEN İZLEMELİ? — en fazla 3.</summary>
        public List<string> WhyWatch { get; set; } = new();
        /// <summary>MAÇIN KİLİDİ.</summary>
        public List<string> KeyBattle { get; set; } = new();
        /// <summary>KADRO ETKİSİ — yalnız doğrulanmış resmî kadro varsa.</summary>
        public List<string> LineupImpact { get; set; } = new();
        /// <summary>BELİRSİZLİK — tek kısa not ya da null.</summary>
        public string? Uncertainty { get; set; }
        /// <summary>OLASI SENARYOLARIN GEREKÇESİ — market adıyla eşleşir.</summary>
        public List<MatchAnalysisScenarioDto> Scenarios { get; set; } = new();
        /// <summary>Analizin süzüldüğü tahmin snapshot kimliği (kartlarla aynı olmalı).</summary>
        public string? SnapshotId { get; set; }
        /// <summary>Çelişki kapısının düşürdüğü cümle sayısı.</summary>
        public int RemovedSentences { get; set; }
    }

    public sealed class MatchAnalysisScenarioDto
    {
        public string Market { get; set; } = string.Empty;
        public string? Support { get; set; }
        public string? Risk { get; set; }
    }

    /// <summary>Hazır analizi DB'den okur — LLM ve dış kaynak ÇAĞIRMAZ.</summary>
    public interface IMatchAnalysisReader
    {
        Task<MatchAnalysisDto> GetAsync(int matchId, CancellationToken ct = default);
    }
}
