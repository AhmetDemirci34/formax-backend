using System;
using System.Collections.Generic;

namespace Formax.Infrastructure.Pipeline;

/// <summary>
/// GDP pipeline'ının uçtan uca çalışma sonucu.
/// Start, aşama sonuçları, genel başarı/başarısızlık ve toplam süre bilgilerini taşır.
/// </summary>
public sealed record PipelineResult
{
    public string? FormaxMatchId { get; init; }

    /// <summary>Pipeline başlangıç zamanı (UTC).</summary>
    public DateTimeOffset StartedAt { get; init; }

    /// <summary>Toplam süre.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Tüm aşamalar başarılı mı? (herhangi biri başarısızsa false)</summary>
    public bool Success { get; init; }

    /// <summary>Aşama-başına sonuçlar (çalışma sırasına göre).</summary>
    public IReadOnlyList<StageResult> Stages { get; init; } = Array.Empty<StageResult>();

    /// <summary>Aşamaların doldurduğu son bağlam.</summary>
    public PipelineContext? Context { get; init; }
}
