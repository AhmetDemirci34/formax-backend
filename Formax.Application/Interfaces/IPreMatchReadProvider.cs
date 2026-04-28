using System.Threading.Tasks;
using Formax.Application.AI.Contexts;

namespace Formax.Application.Interfaces
{
    public interface IPreMatchReadProvider
    {
        Task<PreMatchContext?> ReadAsync(int matchId);
    }
}
