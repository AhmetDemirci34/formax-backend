using System.Collections.Generic;
using Formax.Infrastructure.Merge;
using Formax.Infrastructure.Normalize.Models;

namespace Formax.Infrastructure.Persistence;

/// <summary>Tek bir maçın kalıcılaştırma sonucu.</summary>
public enum GdpPersistStatus
{
    Inserted = 0,
    Updated = 1,
    Unchanged = 2,
    Failed = 3
}

/// <summary>Bir maçın kalıcılaştırma sonucu (durum + varsa hata).</summary>
public sealed record GdpPersistOutcome
{
    public required string FormaxMatchId { get; init; }
    public required GdpPersistStatus Status { get; init; }
    public string? Error { get; init; }
}

/// <summary>Provider kimlik referansı girdisi (persistence katmanına nötr biçimde verilir).</summary>
public sealed record GdpProviderReferenceInput
{
    public required string ProviderName { get; init; }
    public required string ProviderMatchId { get; init; }
}

/// <summary>Kalıcılaştırma isteği: FORMAX Match ID + son MergeResult + provider referansları.</summary>
public sealed record GdpPersistRequest
{
    public required string FormaxMatchId { get; init; }

    /// <summary>Son merge sonucu (Value + Fields/durumlar + ConflictResolutions).</summary>
    public required MergeResult<NormalizedFixture> Merge { get; init; }

    public IReadOnlyList<GdpProviderReferenceInput> ProviderReferences { get; init; } =
        new List<GdpProviderReferenceInput>();
}
