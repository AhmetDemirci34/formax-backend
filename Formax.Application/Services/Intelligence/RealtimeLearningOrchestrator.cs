using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.Services.Intelligence;

public class RealtimeLearningOrchestrator
{
    private readonly UserWeightLearningService _learningService;

    public RealtimeLearningOrchestrator(UserWeightLearningService learningService)
    {
        _learningService = learningService;
    }

    public async Task ProcessInteraction(int userId, string action)
    {
        var profile = await _learningService.GetOrCreate(userId);

        switch (action)
        {
            case "LIKE":
                profile.AffinityWeight += 0.05;
                break;

            case "DISLIKE":
                profile.AffinityWeight -= 0.05;
                break;

            case "PLAY":
                profile.BanditWeight += 0.03;
                break;

            case "SKIP":
                profile.ExplorationWeight += 0.04;
                break;
        }

        // 🔥 clamp (çok kritik)
        profile.AffinityWeight = Clamp(profile.AffinityWeight);
        profile.BanditWeight = Clamp(profile.BanditWeight);
        profile.ExplorationWeight = Clamp(profile.ExplorationWeight);

        await _learningService.Save(profile);
    }

    private double Clamp(double v)
    {
        return Math.Max(0.1, Math.Min(1.5, v));
    }
}
