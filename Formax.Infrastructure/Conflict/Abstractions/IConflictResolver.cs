using System.Collections.Generic;

namespace Formax.Infrastructure.Conflict.Abstractions;

/// <summary>
/// Tek bir çözüm kriteri: aday değerler arasından KENDİ kararını (kazanan + confidence) üretir ya da
/// karar veremezse abstain eder (null).
///
/// Strateji yalnızca kendi kararını verir; başka stratejinin ağırlığını/önceliğini BİLMEZ ve hiçbir
/// hardcoded eşik/ağırlık içermez. Sıra, ağırlık ve eşik <see cref="ConflictConfiguration"/>'dan gelir;
/// engine uygular.
/// </summary>
public interface IConflictResolver<T>
{
    /// <summary>Strateji adı (Configuration sıra/ağırlık eşlemesi ve iz için).</summary>
    string Name { get; }

    /// <summary>Kazanan adayı + bu karara olan confidence'ı döndürür; karar veremiyorsa null.</summary>
    ConflictResolverDecision<T>? Resolve(IReadOnlyList<ConflictCandidate<T>> candidates);
}

/// <summary>Bir stratejinin kararı: seçilen aday ve bu karara olan ham confidence (0..1).</summary>
public sealed record ConflictResolverDecision<T>
{
    public required ConflictCandidate<T> Selected { get; init; }

    public double Confidence { get; init; }
}
