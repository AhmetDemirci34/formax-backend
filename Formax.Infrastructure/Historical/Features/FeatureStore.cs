using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Historical.Features;

/// <summary>Feature Store doldurma özeti (gerçek sayımlarla).</summary>
public sealed record FeatureStoreSummary
{
    public int MatchesTotal { get; init; }
    public int Inserted { get; init; }
    public int Updated { get; init; }
    public int Unchanged { get; init; }
    public long ElapsedMs { get; init; }
    public long PeakMemoryMB { get; init; }
}

/// <summary>Feature Store'u batch dolduran motor sözleşmesi (idempotent).</summary>
public interface IFeatureStoreBuilder
{
    Task<FeatureStoreSummary> BuildAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Probability Engine'in feature'ları OKUDUĞU tek arayüz — DOĞRUDAN Feature Store'dan (Historical'a
/// veya feature hesabına gitmez). Feature Store tek gerçek kaynaktır.
/// </summary>
public interface IFeatureStoreReader
{
    Task<MatchFeatureVector?> ReadAsync(int historicalMatchId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Feature Store okuyucu: <see cref="Formax.Domain.Entities.MatchFeatureRecord"/>'tan feature vektörünü
/// deserialize eder. Probability Engine bunu kullanır (Historical DB'ye dokunmaz).
/// </summary>
public sealed class FeatureStoreReader : IFeatureStoreReader
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly FormaxDbContext _db;

    public FeatureStoreReader(FormaxDbContext db) => _db = db;

    public async Task<MatchFeatureVector?> ReadAsync(int historicalMatchId, CancellationToken cancellationToken = default)
    {
        var json = await _db.MatchFeatureRecords.AsNoTracking()
            .Where(x => x.HistoricalMatchId == historicalMatchId)
            .Select(x => x.FeaturesJson)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        return json is null ? null : JsonSerializer.Deserialize<MatchFeatureVector>(json, Json);
    }
}
