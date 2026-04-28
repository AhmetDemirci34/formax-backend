using System.Threading.Tasks;
using Formax.Application.AI.Contexts;

namespace Formax.Application.Interfaces
{
    public interface ISquadReadProvider
    {
        Task<SquadContext?> ReadAsync(int matchId);
    }
}
