using System;
using System.Collections.Generic;
using Formax.Infrastructure.Merge.Abstractions;

namespace Formax.Infrastructure.Merge.Fields;

/// <summary>
/// Alan-bazlı merge için saf mekanizma (politika içermez).
/// Somut merge stratejileri bu yardımcılarla alan adaylarını okur; kararı stratejinin kendisi verir.
/// </summary>
public static class MergeCandidates
{
    /// <summary>
    /// Bir alan için tüm provider adaylarını katkı sırasına göre listeler (var/yok ayrımıyla).
    /// </summary>
    public static IReadOnlyList<FieldCandidate<TValue>> For<T, TValue>(
        IReadOnlyList<MergeContribution<T>> contributions,
        Func<T, TValue?> selector)
    {
        if (selector is null) throw new ArgumentNullException(nameof(selector));

        var result = new List<FieldCandidate<TValue>>(contributions?.Count ?? 0);
        if (contributions is null)
            return result;

        foreach (var contribution in contributions)
        {
            var value = contribution.Data is null ? default : selector(contribution.Data);
            result.Add(new FieldCandidate<TValue>
            {
                ProviderName = contribution.ProviderName,
                Value = value,
                HasValue = value is not null
            });
        }

        return result;
    }

    /// <summary>
    /// EKSİK ALAN DOLDURMA primitifi: katkı sırasına göre değeri olan İLK provider'ı döndürür.
    /// Yalnızca "eksik olanı mevcut olandan tamamlama" içindir; conflict (aynı alanın farklı değerlerle
    /// birden çok provider'da olması) DURUMUNU çözmez — ilk mevcut değeri döndürür, karar sonraki katmana bırakılır.
    /// </summary>
    public static bool TryFirstPresent<T, TValue>(
        IReadOnlyList<MergeContribution<T>> contributions,
        Func<T, TValue?> selector,
        out TValue? value,
        out string providerName)
    {
        if (selector is null) throw new ArgumentNullException(nameof(selector));

        value = default;
        providerName = string.Empty;
        if (contributions is null)
            return false;

        foreach (var contribution in contributions)
        {
            var candidate = contribution.Data is null ? default : selector(contribution.Data);
            if (candidate is not null)
            {
                value = candidate;
                providerName = contribution.ProviderName;
                return true;
            }
        }

        return false;
    }
}
