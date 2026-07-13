namespace Formax.Infrastructure.Pipeline;

/// <summary>
/// GDP pipeline aşamaları. Enum sırası aynı zamanda çalışma sırasıdır.
/// </summary>
public enum PipelineStage
{
    Provider = 0,
    Normalize = 1,
    MatchIdentity = 2,
    Merge = 3,
    Conflict = 4,
    Coverage = 5,
    Persist = 6
}
