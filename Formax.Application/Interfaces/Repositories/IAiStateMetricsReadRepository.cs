using System;
using System.Threading.Tasks;
using Formax.Domain.States;

namespace Formax.Application.Interfaces.Repositories
{
    public interface IAiStateMetricsReadRepository
    {
        Task<int> TotalAsync();
        Task<int> CountTotalSinceAsync(DateTime sinceUtc);

        Task<int> CountByStateAsync(AIUxState state);
        Task<int> CountByStateSinceAsync(AIUxState state, DateTime sinceUtc);
    }
}
