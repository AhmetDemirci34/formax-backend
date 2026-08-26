using Formax.Engine.Core.ExternalTrends;

namespace Formax.Application.Services.Intelligence;

public class ExternalTrendService
{
    private readonly ExternalTrendEngine _engine;

    public ExternalTrendService(ExternalTrendEngine engine)
    {
        _engine = engine;
    }

    public ExternalTrendDto Get(int matchId)
    {
        var e = _engine.Calculate(matchId);

        // (PERF) Aday maç başına bir Console.WriteLine vardı — feed başına 100 senkron
        // konsol yazımı. Tanılama değeri yok, kaldırıldı.

        // 🔥 fallback
        if (e == null)
        {
            return new ExternalTrendDto
            {
                OddsMovement = 0.2,
                MarketConfidence = 0.5,
                IsHot = false
            };
        }

        // 🔥 SAFE NORMALIZATION
        var movement = Math.Clamp(e.OddsMovement, 0, 1);
        var confidence = Math.Clamp(e.MarketConfidence, 0, 1);

        return new ExternalTrendDto
        {
            OddsMovement = movement,
            MarketConfidence = confidence,
            IsHot = e.IsHot
        };
    }
}