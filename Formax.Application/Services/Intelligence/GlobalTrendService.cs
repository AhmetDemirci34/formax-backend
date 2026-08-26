using Formax.Application.Abstractions;
using Formax.Application.DTOs.Home;
using Formax.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Formax.Application.Services.Intelligence;

public class GlobalTrendService
{
    private readonly IAppDbContext _context;

    public GlobalTrendService(IAppDbContext context)
    {
        _context = context;
    }

    // PERF (MVP freeze): GetGlobalScore, öneri motorunun 100 aday maçının HER BİRİ için
    // ayrı bir UserActions sorgusu atıyordu. Servis Scoped olduğundan aşağıdaki ön-yükleme
    // İSTEK BAŞINA doldurulur ve aynı satırları (maç başına CreatedAt DESC ilk 100) tek
    // sorguda getirir. Ön-yükleme yapılmamışsa eski tekil sorgu yolu aynen çalışır →
    // skor formülü ve sonuç DEĞİŞMEZ.
    private Dictionary<int, List<UserAction>>? _preloadedByMatch;

    public async Task PreloadForMatchesAsync(IReadOnlyCollection<int> matchIds)
    {
        if (matchIds is null || matchIds.Count == 0)
        {
            _preloadedByMatch = new Dictionary<int, List<UserAction>>();
            return;
        }

        var rows = await _context.UserActions
            .AsNoTracking()
            .Where(x => matchIds.Contains(x.MatchId))
            .ToListAsync();

        _preloadedByMatch = rows
            .GroupBy(x => x.MatchId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.CreatedAt).Take(100).ToList());
    }

    public async Task<double> GetGlobalScore(HomeRadarMatchDto match)
    {
        var now = DateTime.UtcNow;

        List<UserAction> actions;

        if (_preloadedByMatch is not null)
        {
            actions = _preloadedByMatch.TryGetValue(match.MatchId, out var pre)
                ? pre
                : new List<UserAction>();
        }
        else
        {
            actions = await _context.UserActions
                .AsNoTracking()
                .Where(x => x.MatchId == match.MatchId)
                .OrderByDescending(x => x.CreatedAt)
                .Take(100)
                .ToListAsync();
        }

        double baseScore;

        if (!actions.Any())
        {
            var play = match.PlayRate / 100.0;
            var radar = match.RadarScore / 100.0;

            baseScore = 0.45 + ((play + radar) * 0.1);
        }
        else
        {
            double score = 0;

            foreach (var a in actions)
            {
                // ActionType-based weighting (replaces binary ==1 ? +1 : -1).
                // Codes: 1=like, 3=follow, 2=detail, 0=view, -1=skip.
                var actionScore = a.ActionType switch
                {
                    1  =>  1.0,   // like
                    3  =>  0.8,   // follow
                    2  =>  0.5,   // detail open
                    0  =>  0.0,   // view (neutral)
                    -1 => -1.0,   // skip
                    _  =>  0.0
                };

                var ageMin = (now - a.CreatedAt).TotalMinutes;

                var timeWeight = Math.Exp(-ageMin / 60.0);

                score += actionScore * timeWeight;
            }

            var scaled = Math.Tanh(score / 3.0);

            baseScore = 0.5 + (scaled * 0.5);
        }

        // 🔥 DOĞRU AMPLIFICATION (linear boost)
        var boosted = baseScore + ((baseScore - 0.5) * 0.8);

        return Math.Round(Math.Clamp(boosted, 0.0, 1.0), 3);
    }
}