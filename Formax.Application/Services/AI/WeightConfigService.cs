using Formax.Domain.Entities;
using Formax.Application.Interfaces;

namespace Formax.Application.Services.AI;

public class WeightConfigService
{
    private readonly IAIWeightConfigRepository _repo;

    private AIWeightConfig? _cache;
    private DateTime _cacheTime;

    public WeightConfigService(IAIWeightConfigRepository repo)
    {
        _repo = repo;
    }

    public async Task<AIWeightConfig> GetActiveAsync()
    {
        // CACHE (5 dk)
        if (_cache != null && (DateTime.UtcNow - _cacheTime).TotalMinutes < 5)
            return Normalize(_cache);

        var config = await _repo.GetActiveAsync();

        // FALLBACK (FAIL-SAFE)
        if (config == null)
        {
            config = new AIWeightConfig
            {
                InterestWeight = 1.0,
                BanditWeight = 0.2,
                SessionWeight = 0.3,
                TrendWeight = 0.5,// 🔥 TEST İÇİN YÜKSELTİLDİ
                DiversityWeight = 0.1,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
        }

        config = Normalize(config);

        _cache = config;
        _cacheTime = DateTime.UtcNow;

        return config;
    }

    private AIWeightConfig Normalize(AIWeightConfig config)
    {
        // 🔥 PIPELINE GUARD (0 YOK)

        if (config.InterestWeight <= 0) config.InterestWeight = 0.0001;
        if (config.BanditWeight <= 0) config.BanditWeight = 0.0001;
        if (config.SessionWeight <= 0) config.SessionWeight = 0.0001;
        if (config.TrendWeight <= 0) config.TrendWeight = 0.0001;
        if (config.DiversityWeight <= 0) config.DiversityWeight = 0.0001;

        return config;
    }
}