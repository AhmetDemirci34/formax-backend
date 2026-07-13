using System;
using System.Collections.Generic;

namespace Formax.Infrastructure.Conflict;

/// <summary>
/// Bir alan çatışmasının çözüm sonucu.
/// Çözülemezse <see cref="IsResolved"/> = false olur; Conflict durumu korunur ve tüm adaylar
/// <see cref="Candidates"/> içinde saklanır (veri kaybı yok).
/// </summary>
public sealed record ConflictResolution
{
    public required string Field { get; init; }

    public object? SelectedValue { get; init; }

    public string? SelectedProvider { get; init; }

    public string? ResolutionStrategy { get; init; }

    public double Confidence { get; init; }

    /// <summary>Tüm adaylar (provenance) — çözülse de çözülmese de korunur.</summary>
    public IReadOnlyList<ConflictCandidate<object>> Candidates { get; init; } = Array.Empty<ConflictCandidate<object>>();

    public bool IsResolved { get; init; }

    public static ConflictResolution Resolved(
        string field,
        ConflictCandidate<object> selected,
        string strategy,
        double confidence,
        IReadOnlyList<ConflictCandidate<object>> candidates) => new()
    {
        Field = field,
        SelectedValue = selected.Value,
        SelectedProvider = selected.ProviderName,
        ResolutionStrategy = strategy,
        Confidence = confidence,
        Candidates = candidates,
        IsResolved = true
    };

    public static ConflictResolution Unresolved(
        string field,
        IReadOnlyList<ConflictCandidate<object>> candidates) => new()
    {
        Field = field,
        Candidates = candidates,
        IsResolved = false
    };
}
