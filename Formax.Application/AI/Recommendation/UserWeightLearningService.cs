using Formax.Application.Interfaces;
using Formax.Domain.Entities;

namespace Formax.Application.Services.Intelligence;

public class UserWeightLearningService
{
    private readonly IUserWeightProfileRepository _repo;

    public UserWeightLearningService(IUserWeightProfileRepository repo)
    {
        _repo = repo;
    }

    public async Task<UserWeightProfile> GetOrCreate(int userId)
    {
        var profile = await _repo.Get(userId);

        if (profile != null)
            return profile;

        profile = new UserWeightProfile
        {
            UserId = userId,
            AffinityWeight = 1.0,
            BanditWeight = 1.0,
            ExplorationWeight = 1.0,
            UpdatedAtUtc = DateTime.UtcNow
        };

        await _repo.Save(profile);
        return profile;
    }

    public async Task Save(UserWeightProfile profile)
    {
        profile.UpdatedAtUtc = DateTime.UtcNow;
        await _repo.Save(profile);
    }

    // 🔥 BACKWARD COMPATIBILITY (HATA FIX)
    public async Task Update(int userId, double reward)
    {
        var profile = await GetOrCreate(userId);

        profile.AffinityWeight += reward * 0.01;
        profile.BanditWeight += reward * 0.005;

        Normalize(profile);
        Clamp(profile);

        await Save(profile);
    }

    // 🔥 FINAL AI LEARNING
    public async Task UpdateFromEvent(int userId, FeedInteractionEvent e)
    {
        var profile = await GetOrCreate(userId);

        ApplyTimeDecay(profile);

        var reward = GetReward(e);

        profile.AffinityWeight += reward.Affinity;
        profile.BanditWeight += reward.Bandit;
        profile.ExplorationWeight += reward.Exploration;

        Normalize(profile);
        Clamp(profile);

        await Save(profile);
    }

    private (double Affinity, double Bandit, double Exploration) GetReward(FeedInteractionEvent e)
    {
        return e.SignalType switch
        {
            FeedSignalType.Click => (+0.06, +0.02, -0.01),
            FeedSignalType.Dwell => (+0.04, 0, -0.01),
            FeedSignalType.Follow => (+0.12, +0.04, -0.02),
            FeedSignalType.Skip => (-0.05, 0, +0.05),
            _ => (0, 0, 0)
        };
    }

    private void ApplyTimeDecay(UserWeightProfile p)
    {
        var hours = (DateTime.UtcNow - p.UpdatedAtUtc).TotalHours;

        if (hours < 6)
            return;

        var decay = 0.98;

        p.AffinityWeight *= decay;
        p.BanditWeight *= decay;
        p.ExplorationWeight *= decay;
    }

    private void Normalize(UserWeightProfile p)
    {
        var total = p.AffinityWeight + p.BanditWeight + p.ExplorationWeight;

        if (total == 0)
            return;

        p.AffinityWeight = (p.AffinityWeight / total) * 3;
        p.BanditWeight = (p.BanditWeight / total) * 2;
        p.ExplorationWeight = (p.ExplorationWeight / total) * 1.5;
    }

    private void Clamp(UserWeightProfile p)
    {
        p.AffinityWeight = Math.Clamp(p.AffinityWeight, 0.2, 2.5);
        p.BanditWeight = Math.Clamp(p.BanditWeight, 0.2, 2.0);
        p.ExplorationWeight = Math.Clamp(p.ExplorationWeight, 0.2, 1.5);
    }
}