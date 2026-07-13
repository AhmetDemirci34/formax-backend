using Formax.Infrastructure.Coverage.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Formax.Infrastructure.Coverage;

/// <summary>
/// Coverage Engine DI kayıtları.
///
/// Bir kategori analizörü eklemek = (1) <see cref="ICoverageAnalyzer"/> uygulayan sınıf yaz,
/// (2) tek satırla kaydet: <c>services.AddCoverageAnalyzer&lt;FixtureCoverageAnalyzer&gt;();</c>
/// Engine onu otomatik keşfeder ve ilgili kategoriyi doldurur.
/// </summary>
public static class CoverageServiceCollectionExtensions
{
    public static IServiceCollection AddCoverageEngine(this IServiceCollection services)
    {
        services.AddScoped<CoverageEngine>();

        // Kategori analizörleri buraya, sonraki fazda:
        // services.AddCoverageAnalyzer<FixtureCoverageAnalyzer>();
        // services.AddCoverageAnalyzer<LineupsCoverageAnalyzer>();

        return services;
    }

    /// <summary>Bir kategori analizörü kaydeder. İstenildiği kadar çağrılabilir; limit yoktur.</summary>
    public static IServiceCollection AddCoverageAnalyzer<TAnalyzer>(this IServiceCollection services)
        where TAnalyzer : class, ICoverageAnalyzer
    {
        services.AddScoped<ICoverageAnalyzer, TAnalyzer>();
        return services;
    }
}
