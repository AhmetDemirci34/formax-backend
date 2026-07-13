namespace Formax.Infrastructure.Normalize.Abstractions;

/// <summary>
/// Ortak HAM modeli (<typeparamref name="TRaw"/>) FORMAX ortak modeli (<typeparamref name="TModel"/>)'e
/// dönüştüren sözleşme.
///
/// Provider-BAĞIMSIZDIR: normalizer hangi provider'dan geldiğini bilmez, yalnızca ortak HAM modelle çalışır.
/// Her (HAM, Normalized) çifti için TEK normalizer vardır (Normalize Engine tektir).
/// </summary>
public interface INormalizer<TRaw, TModel>
{
    /// <summary>Ortak HAM modeli ortak Normalized modele dönüştürür.</summary>
    TModel Normalize(TRaw raw);
}
