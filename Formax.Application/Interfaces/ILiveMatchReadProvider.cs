using Formax.Application.AI.Contexts;

namespace Formax.Application.Interfaces
{
    public interface ILiveMatchReadProvider
    {
        LiveMatchContext Read(int matchId);
    }
}
