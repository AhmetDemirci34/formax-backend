using Formax.Infrastructure.Pipeline.Abstractions;
using Formax.Infrastructure.Pipeline.Stages;
using Microsoft.Extensions.DependencyInjection;

namespace Formax.Infrastructure.Pipeline;

/// <summary>
/// GDP Pipeline DI kayıtları.
/// Aşamalar <see cref="IPipelineStage"/> olarak kaydedilir; pipeline hepsini sıraya göre çalıştırır.
/// Yeni bir aşama eklemek = sınıf yaz + <c>AddPipelineStage&lt;T&gt;()</c>.
///
/// NOT: Bu, altı engine'in (Provider/Normalize/MatchIdentity/Merge/Conflict/Coverage) DI'da zaten
/// kayıtlı olmasını varsayar; aşamalar ilgili motorları buradan çözer.
/// </summary>
public static class GlobalDataPipelineServiceCollectionExtensions
{
    public static IServiceCollection AddGlobalDataPipeline(this IServiceCollection services)
    {
        services.AddScoped<IGlobalDataPipeline, GlobalDataPipeline>();

        services.AddPipelineStage<ProviderStage>();
        services.AddPipelineStage<NormalizeStage>();
        services.AddPipelineStage<MatchIdentityStage>();
        services.AddPipelineStage<MergeStage>();
        services.AddPipelineStage<ConflictStage>();
        services.AddPipelineStage<CoverageStage>();
        services.AddPipelineStage<PersistStage>();

        return services;
    }

    public static IServiceCollection AddPipelineStage<TStage>(this IServiceCollection services)
        where TStage : class, IPipelineStage
    {
        services.AddScoped<IPipelineStage, TStage>();
        return services;
    }
}
