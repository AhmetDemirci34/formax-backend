using System;
using System.Collections.Generic;
using System.Linq;

namespace Formax.Infrastructure.MatchIdentity;

/// <summary>
/// Çözümlenmiş bir maç kimliği: tek bir <see cref="FormaxMatchId"/> ve ona bağlı tüm provider referansları.
/// Bir maç birden fazla <see cref="ProviderMatchReference"/> (dolayısıyla birden fazla Provider Match ID) taşıyabilir.
/// </summary>
public sealed record MatchIdentityResult
{
    public required FormaxMatchId FormaxMatchId { get; init; }

    /// <summary>Bu FORMAX kimliğine ilişkilendirilmiş provider referansları.</summary>
    public required IReadOnlyList<ProviderMatchReference> References { get; init; }

    /// <summary>Bu çağrıda yeni bir FORMAX kimliği mi üretildi?</summary>
    public bool IsNew { get; init; }

    /// <summary>Son ilişkilendirmenin toplam sayısal skoru (Confidence).</summary>
    public double Score { get; init; }

    /// <summary>Skordan türetilen güven kovası.</summary>
    public MatchIdentityConfidence Confidence { get; init; } = MatchIdentityConfidence.None;

    /// <summary>Eşleştirmeyi sağlayan yaklaşım (varsa).</summary>
    public string? MatchedBy { get; init; }

    /// <summary>Tek bir referanstan yeni bir kimlik (anchor) oluşturur.</summary>
    public static MatchIdentityResult NewIdentity(FormaxMatchId id, ProviderMatchReference reference) => new()
    {
        FormaxMatchId = id,
        References = new[] { reference ?? throw new ArgumentNullException(nameof(reference)) },
        IsNew = true,
        Score = 0d,
        Confidence = MatchIdentityConfidence.None
    };

    /// <summary>
    /// Bu kimliğe yeni bir provider referansını İLİŞKİLENDİRİR (yapısal birleştirme — veri MERGE değildir).
    /// Aynı (provider, id) referansı zaten varsa tekrar eklenmez (basit idempotent; duplicate-silme değil).
    /// </summary>
    public MatchIdentityResult Associate(
        ProviderMatchReference reference,
        double score,
        string strategy)
    {
        if (reference is null) throw new ArgumentNullException(nameof(reference));

        var alreadyPresent = References.Any(r =>
            string.Equals(r.ProviderName, reference.ProviderName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(r.ProviderMatchId, reference.ProviderMatchId, StringComparison.Ordinal));

        var references = alreadyPresent
            ? References
            : References.Append(reference).ToList();

        return this with
        {
            References = references,
            IsNew = false,
            Score = score,
            Confidence = ToConfidence(score),
            MatchedBy = strategy
        };
    }

    /// <summary>Sayısal skoru güven kovasına çevirir.</summary>
    public static MatchIdentityConfidence ToConfidence(double score) => score switch
    {
        >= 1.0d => MatchIdentityConfidence.Exact,
        >= 0.8d => MatchIdentityConfidence.High,
        >= 0.7d => MatchIdentityConfidence.Medium,
        >= 0.4d => MatchIdentityConfidence.Low,
        _ => MatchIdentityConfidence.None
    };
}
