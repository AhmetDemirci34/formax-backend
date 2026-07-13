using System.Collections.Generic;

namespace Formax.Infrastructure.MatchIdentity.Abstractions;

/// <summary>
/// Bir provider referansının hangi FORMAX maçına ait olduğunu çözümleyen sözleşme.
/// Durumsuzdur: bilinen kimlikler dışarıdan verilir, kalıcılık (DB) bu katmanın işi değildir.
/// </summary>
public interface IMatchIdentityResolver
{
    /// <summary>
    /// <paramref name="reference"/>'ı bilinen kimlikler (<paramref name="known"/>) arasında çözer.
    /// Eşleşme bulunursa ilgili kimliğe ilişkilendirir; bulunmazsa yeni bir FORMAX Match ID atar.
    /// </summary>
    MatchIdentityResult Resolve(
        ProviderMatchReference reference,
        IReadOnlyCollection<MatchIdentityResult> known);
}
