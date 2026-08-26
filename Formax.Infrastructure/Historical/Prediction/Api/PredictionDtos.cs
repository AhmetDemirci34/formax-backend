using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Formax.Infrastructure.Historical.Prediction.Api;

/// <summary>Aktif model değiştirme (switch/rollback) isteği.</summary>
public sealed class ActivateModelRequest
{
    [Required(ErrorMessage = "VersionId zorunludur.")]
    public string VersionId { get; set; } = string.Empty;
}

/// <summary>Tahmin isteği DTO'su. Validasyon: MatchId pozitif olmalı.</summary>
public sealed class MatchPredictionRequest
{
    /// <summary>Tahmin edilecek maçın HistoricalMatch kimliği (Feature Store anahtarı).</summary>
    [Range(1, int.MaxValue, ErrorMessage = "MatchId pozitif bir tam sayı olmalıdır.")]
    public int MatchId { get; set; }
}

/// <summary>Bir feature'ın tahmine katkısı (response içinde).</summary>
public sealed record FeatureContributionDto
{
    public required string Feature { get; init; }
    public double Value { get; init; }
    /// <summary>Olasılık uzayında işaretli katkı (+ destek, − zayıflatıcı).</summary>
    public double Contribution { get; init; }
}

/// <summary>
/// Tek response altında Probability + Confidence + Explainability. Olasılıklar 1'e toplanır.
/// </summary>
public sealed record MatchPredictionResponse
{
    public int MatchId { get; init; }
    /// <summary>Tahmin edilen sonuç: H (ev), D (beraberlik), A (deplasman).</summary>
    public required string PredictedOutcome { get; init; }

    public double HomeWinProbability { get; init; }
    public double DrawProbability { get; init; }
    public double AwayWinProbability { get; init; }

    public double ConfidenceScore { get; init; }
    public required string ConfidenceLevel { get; init; }

    public required IReadOnlyList<FeatureContributionDto> TopFeatures { get; init; }
    public required string Explanation { get; init; }

    public required string ModelVersion { get; init; }
    public DateTime PredictionTimestamp { get; init; }
}
