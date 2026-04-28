using System.Threading;
using System.Threading.Tasks;
using Formax.Domain.States;

namespace Formax.Application.Interfaces.Repositories
{
    public interface IAIAnalysisWriteRepository
    {
        Task PersistLastExtendedContextKeyAsync(
            int matchId,
            AIContextKey contextKey,
            CancellationToken cancellationToken);
    }
}
