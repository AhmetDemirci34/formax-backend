using System.Threading.Tasks;

namespace Formax.Application.Interfaces
{
    public interface IExternalTrendProvider
    {
        Task<double> GetMarketMomentum(int matchId);
    }
}