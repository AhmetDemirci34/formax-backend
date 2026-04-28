using System.Threading.Tasks;
using Formax.Application.AI.Contexts;
using Formax.Application.Interfaces;

namespace Formax.Infrastructure.ReadProviders
{
    public sealed class SquadReadProvider : ISquadReadProvider
    {
        public Task<SquadContext?> ReadAsync(int matchId)
        {
            // FAZ-10.2: Veri kaynağı yok.
            // Skeleton dönüş: context yok (null).
            return Task.FromResult<SquadContext?>(null);
        }
    }
}
