using System.Collections.Concurrent;

namespace Formax.Application.Services.Intelligence;

public class OddsMovementService
{
    private readonly ConcurrentDictionary<int, OddsSnapshot> _cache = new();

    public void UpdateOdds(int matchId, double openingOdds, double currentOdds)
    {
        _cache[matchId] = new OddsSnapshot
        {
            OpeningOdds = openingOdds,
            CurrentOdds = currentOdds,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public double GetMovementScore(int matchId)
    {
        if (!_cache.TryGetValue(matchId, out var odds))
            return 0;

        var diff = odds.OpeningOdds - odds.CurrentOdds;

        // 🔥 düşüş = pozitif (para giriyor)
        // yükseliş = negatif
        var movement = diff / odds.OpeningOdds;

        return Math.Clamp(movement, -1, 1);
    }
}

public class OddsSnapshot
{
    public double OpeningOdds { get; set; }
    public double CurrentOdds { get; set; }
    public DateTime UpdatedAt { get; set; }
}