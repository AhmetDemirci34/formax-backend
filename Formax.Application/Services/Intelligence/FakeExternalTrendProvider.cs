using Formax.Application.Interfaces;
using System;
using System.Threading.Tasks;

namespace Formax.Application.Services.Intelligence
{
    public class FakeExternalTrendProvider : IExternalTrendProvider
    {
        public Task<double> GetMarketMomentum(int matchId)
        {
            var r = new Random(matchId);
            return Task.FromResult(0.3 + (r.NextDouble() * 0.7)); // 0.3 - 1.0
        }
    }
}