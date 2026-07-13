using System;
using Formax.Infrastructure.Normalize.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Formax.Infrastructure.Normalize;

/// <summary>
/// Normalize Engine giriş noktası — TEK'tir ve provider-BAĞIMSIZDIR.
/// Yalnızca ortak HAM modelle (<typeparamref name="TRaw"/>) çalışır; hangi provider'ın ürettiğini bilmez.
/// Provider ham çıktısını ortak HAM modele dönüştürmek Provider Mapper katmanının işidir
/// (bkz. <see cref="Mapping.IProviderMapper{TRaw}"/>).
///
/// Akış: Provider JSON → Provider Mapper → Ortak HAM Model → (Normalize Engine) → Normalized Model.
///
/// KAPSAM DIŞI: Match Identity / Merge / Conflict / Coverage / Data Quality burada YOKTUR.
/// </summary>
public sealed class NormalizeEngine
{
    private readonly IServiceProvider _services;

    public NormalizeEngine(IServiceProvider services)
    {
        _services = services;
    }

    /// <summary>HAM modeli Normalized modele dönüştürür. Normalizer yoksa <see cref="NotSupportedException"/> fırlatır.</summary>
    public TModel Normalize<TRaw, TModel>(TRaw raw)
    {
        if (raw is null) throw new ArgumentNullException(nameof(raw));

        var normalizer = _services.GetService<INormalizer<TRaw, TModel>>()
            ?? throw new NotSupportedException(
                $"{typeof(TRaw).Name} → {typeof(TModel).Name} için normalizer kayıtlı değil.");

        return normalizer.Normalize(raw);
    }

    /// <summary>Normalizer varsa dönüştürür; yoksa false döner (istisna fırlatmaz).</summary>
    public bool TryNormalize<TRaw, TModel>(TRaw raw, out TModel? model)
    {
        model = default;
        if (raw is null) return false;

        var normalizer = _services.GetService<INormalizer<TRaw, TModel>>();
        if (normalizer is null) return false;

        model = normalizer.Normalize(raw);
        return true;
    }

    /// <summary>Bu (HAM, Normalized) çifti için normalizer kayıtlı mı?</summary>
    public bool CanNormalize<TRaw, TModel>() =>
        _services.GetService<INormalizer<TRaw, TModel>>() is not null;
}
