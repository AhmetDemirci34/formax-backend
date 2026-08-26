using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.DTOs.Radar;

namespace Formax.Application.Services.Radar.Feed
{
    /// <summary>
    /// Radar Feed (R.13.2) — query service exposing feed insights as DTOs for the API.
    /// Builds via <see cref="IFeedInsightBuilder"/>, orders by importance, caps the list.
    /// No ranking algorithm, no learning — just filter + sort + limit.
    /// </summary>
    public interface IFeedInsightQueryService
    {
        Task<IReadOnlyList<FeedInsightDto>> GetFeedAsync(int limit = 50, CancellationToken ct = default);

        /// <summary>
        /// Yalnız verilen maçların insight'ları (öneri feed'i adaylarını zaten biliyor).
        /// Limit yok — istenen id kümesi zaten sınırlıdır.
        /// </summary>
        Task<IReadOnlyList<FeedInsightDto>> GetFeedByMatchIdsAsync(
            IReadOnlyCollection<int> matchIds, CancellationToken ct = default);
    }
}
