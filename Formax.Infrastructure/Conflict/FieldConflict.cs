using System.Collections.Generic;

namespace Formax.Infrastructure.Conflict;

/// <summary>
/// Tek bir alan için birden fazla aday değeri barındıran yapı (çözülecek çatışma girdisi).
/// </summary>
public sealed record FieldConflict<T>
{
    /// <summary>Alan adı/yolu (ör. "Venue.Capacity").</summary>
    public required string Field { get; init; }

    /// <summary>Aynı alan için gelen provider adayları.</summary>
    public required IReadOnlyList<ConflictCandidate<T>> Candidates { get; init; }
}
