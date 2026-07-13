using System;
using System.Collections.Generic;
using Formax.Infrastructure.Merge.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Formax.Infrastructure.Merge;

/// <summary>
/// Merge Engine giriş noktası.
/// Aynı FORMAX Match ID altındaki çoklu provider katkısını, hedef model türü için kayıtlı
/// <see cref="IMergeStrategy{T}"/>'ye devrederek tek bir maç verisine birleştirir.
///
/// Bu faz İSKELET: hiçbir somut merge stratejisi kayıtlı değildir; strateji gelene kadar
/// <see cref="Merge{T}"/> desteklenmediğini bildirir. Gerçek birleştirme kuralları, alan seçimi,
/// eksik doldurma politikası stratejilerde yazılacaktır.
///
/// KAPSAM DIŞI: Conflict çözme / Duplicate silme / Coverage hesaplama / Data Quality / DB burada YOKTUR.
/// </summary>
public sealed class MergeEngine
{
    private readonly IServiceProvider _services;

    public MergeEngine(IServiceProvider services)
    {
        _services = services;
    }

    /// <summary>Uygun stratejiyle birleştirir. Strateji yoksa <see cref="NotSupportedException"/> fırlatır.</summary>
    public MergeResult<T> Merge<T>(IReadOnlyList<MergeContribution<T>> contributions, MergeContext context)
    {
        if (contributions is null) throw new ArgumentNullException(nameof(contributions));
        context ??= new MergeContext();

        var strategy = _services.GetService<IMergeStrategy<T>>()
            ?? throw new NotSupportedException(
                $"{typeof(T).Name} için kayıtlı bir merge stratejisi yok.");

        return strategy.Merge(contributions, context);
    }

    /// <summary>Strateji varsa birleştirir; yoksa false döner (istisna fırlatmaz).</summary>
    public bool TryMerge<T>(IReadOnlyList<MergeContribution<T>> contributions, MergeContext context, out MergeResult<T>? result)
    {
        result = null;
        if (contributions is null)
            return false;

        var strategy = _services.GetService<IMergeStrategy<T>>();
        if (strategy is null)
            return false;

        result = strategy.Merge(contributions, context ?? new MergeContext());
        return true;
    }

    /// <summary>Bu model türü için bir merge stratejisi kayıtlı mı?</summary>
    public bool CanMerge<T>() => _services.GetService<IMergeStrategy<T>>() is not null;
}
