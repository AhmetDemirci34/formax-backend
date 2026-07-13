using System;

namespace Formax.Infrastructure.Pipeline;

/// <summary>
/// Tek bir aşamanın çalışma sonucu (başarı/başarısızlık + zamanlama + hata).
/// </summary>
public sealed record StageResult
{
    public required PipelineStage Stage { get; init; }

    public bool Success { get; init; }

    /// <summary>Başarısızsa hata mesajı; başarılıysa null.</summary>
    public string? Error { get; init; }

    public DateTimeOffset StartedAt { get; init; }

    public TimeSpan Duration { get; init; }
}
