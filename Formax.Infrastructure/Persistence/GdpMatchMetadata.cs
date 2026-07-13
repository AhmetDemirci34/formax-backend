using System;
using System.Collections.Generic;

namespace Formax.Infrastructure.Persistence;

/// <summary>
/// FORMAX Match ID ile mevcut Domain <c>Match</c> arasındaki bağ (GDP metadata).
/// GDP kendi maç aggregate'ını OLUŞTURMAZ; bu link, hangi FORMAX kimliğinin hangi Domain Match'e karşılık
/// geldiğini tutar → aynı FormaxMatchId için duplicate önlenir.
/// </summary>
public class GdpMatchLink
{
    public string FormaxMatchId { get; set; } = null!;

    /// <summary>Beslenen mevcut Domain <c>Match</c>'in Id'si.</summary>
    public int MatchId { get; set; }

    /// <summary>
    /// Fikstürün ham tur/etiket bilgisi (ör. "Final", "Round of 16", "Achtelfinale").
    /// PreMatch çerçeve türetiminin (knockout/final/importance) girdisidir; NULL ise
    /// türetim yalnızca lig adına düşer. Domain Match'te tur alanı yoktur → burada tutulur.
    /// </summary>
    public string? Round { get; set; }

    public DateTimeOffset LastUpdatedUtc { get; set; }

    public List<GdpProviderFieldProvenance> Provenance { get; set; } = new();
    public List<GdpConflictResolution> ConflictResolutions { get; set; } = new();
    public List<GdpProviderMatchReference> ProviderReferences { get; set; } = new();
}

/// <summary>Bir alanın merge durumu (Merged/Missing/Conflict) ve hangi provider'dan geldiği (provenance).</summary>
public class GdpProviderFieldProvenance
{
    public int Id { get; set; }
    public string FormaxMatchId { get; set; } = null!;
    public string Field { get; set; } = null!;
    public string State { get; set; } = null!;
    public string? Provider { get; set; }
}

/// <summary>Bir alan çatışmasının çözüm bilgisi.</summary>
public class GdpConflictResolution
{
    public int Id { get; set; }
    public string FormaxMatchId { get; set; } = null!;
    public string Field { get; set; } = null!;
    public string? SelectedProvider { get; set; }
    public string? ResolutionStrategy { get; set; }
    public double Confidence { get; set; }
    public bool IsResolved { get; set; }
}

/// <summary>Bu FORMAX maçına bağlı provider kimliği (provider + o provider'ın maç id'si).</summary>
public class GdpProviderMatchReference
{
    public int Id { get; set; }
    public string FormaxMatchId { get; set; } = null!;
    public string ProviderName { get; set; } = null!;
    public string ProviderMatchId { get; set; } = null!;
}
