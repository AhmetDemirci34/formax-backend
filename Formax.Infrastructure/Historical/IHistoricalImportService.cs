using System.Threading;
using System.Threading.Tasks;

namespace Formax.Infrastructure.Historical;

/// <summary>
/// Tarihsel CSV import sözleşmesi. İdempotent (SourceKey dedup): ikinci çalıştırmada yeni kayıt oluşmaz.
/// </summary>
public interface IHistoricalImportService
{
    Task<HistoricalImportSummary> ImportAsync(string matchesCsvPath, string eloCsvPath, CancellationToken cancellationToken = default);
}
