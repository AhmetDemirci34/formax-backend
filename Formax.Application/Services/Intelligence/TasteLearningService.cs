using Formax.Domain.Entities;
using Formax.Application.Interfaces;

namespace Formax.Application.Services.Intelligence;

public class TasteLearningService
{
    private readonly IUserTasteVectorRepository _repo;

    public TasteLearningService(IUserTasteVectorRepository repo)
    {
        _repo = repo;
    }

    public async Task<UserTasteVector> GetOrCreate(int userId)
    {
        var v = await _repo.Get(userId);

        if (v != null)
            return v;

        v = new UserTasteVector
        {
            UserId = userId
        };

        await _repo.Save(v);
        return v;
    }

    public async Task UpdateFromEvent(FeedInteractionEvent e)
    {
        var v = await GetOrCreate(e.UserId);

        ApplyDecay(v);

        var reward = GetReward(e);

        Apply(v.MatchAffinity, e.MatchId, reward);

        Normalize(v);

        v.UpdatedAtUtc = DateTime.UtcNow;

        await _repo.Save(v);
    }

    private double GetReward(FeedInteractionEvent e)
    {
        return e.SignalType switch
        {
            FeedSignalType.Click => 0.1,
            FeedSignalType.Follow => 0.2,
            FeedSignalType.Dwell => 0.08,
            FeedSignalType.Skip => -0.12,
            _ => 0
        };
    }

    private void Apply(Dictionary<int, double> map, int key, double value)
    {
        if (!map.ContainsKey(key))
            map[key] = 0;

        map[key] += value;
    }

    private void ApplyDecay(UserTasteVector v)
    {
        var hours = (DateTime.UtcNow - v.UpdatedAtUtc).TotalHours;

        if (hours < 6)
            return;

        foreach (var key in v.MatchAffinity.Keys.ToList())
        {
            v.MatchAffinity[key] *= 0.98;
        }
    }

    private void Normalize(UserTasteVector v)
    {
        if (v.MatchAffinity.Count == 0)
            return;

        var total = v.MatchAffinity.Values.Sum(x => Math.Abs(x));

        if (total == 0)
            return;

        var keys = v.MatchAffinity.Keys.ToList();

        foreach (var k in keys)
        {
            var val = (v.MatchAffinity[k] / total) * 3;
            v.MatchAffinity[k] = Math.Clamp(val, -2, 2);
        }
    }
}