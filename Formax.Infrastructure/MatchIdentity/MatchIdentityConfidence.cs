namespace Formax.Infrastructure.MatchIdentity;

/// <summary>
/// Bir provider referansının belirli bir FORMAX maçına ait olma güven düzeyi.
/// Eşleştirme stratejileri bu ölçeği üretir; Engine eşik ile karşılaştırır.
/// </summary>
public enum MatchIdentityConfidence
{
    None = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Exact = 4
}
