using System.Collections.Generic;
using System.Linq;

namespace Formax.Infrastructure.Historical.Dataset;

public enum ValidationVerdict { Pass, Warning, Fail }

/// <summary>Tek bir doğrulama kontrolünün sonucu.</summary>
public sealed record ValidationCheck
{
    public required string Name { get; init; }
    public required ValidationVerdict Verdict { get; init; }
    public required string Detail { get; init; }
}

/// <summary>Dataset v1 doğrulama raporu. Kritik hata (FAIL) yoksa üretime hazırdır.</summary>
public sealed record DatasetValidationReport
{
    public required IReadOnlyList<ValidationCheck> Checks { get; init; }

    public ValidationVerdict Overall =>
        Checks.Any(c => c.Verdict == ValidationVerdict.Fail) ? ValidationVerdict.Fail
        : Checks.Any(c => c.Verdict == ValidationVerdict.Warning) ? ValidationVerdict.Warning
        : ValidationVerdict.Pass;

    /// <summary>FAIL yoksa Probability Engine için hazır (WARNING kabul edilebilir).</summary>
    public bool ReadyForProbabilityEngine => Checks.All(c => c.Verdict != ValidationVerdict.Fail);
}
