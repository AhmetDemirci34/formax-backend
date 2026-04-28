using Formax.Application.AI.Contexts;

namespace Formax.Application.Interfaces
{
    public interface IWorldPerceptionReadProvider
    {
        WorldPerceptionContext Read(int matchId);
    }
}
