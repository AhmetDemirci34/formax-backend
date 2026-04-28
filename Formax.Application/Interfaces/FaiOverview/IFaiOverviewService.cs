using Formax.Application.DTOs.FaiOverview;

namespace Formax.Application.Interfaces.FaiOverview;

public interface IFaiOverviewService
{
    Task<List<FaiOverviewItemDto>> GetOverviewAsync(CancellationToken cancellationToken = default);
    Task<FaiStateDto> GetStateAsync(CancellationToken cancellationToken = default);
}

