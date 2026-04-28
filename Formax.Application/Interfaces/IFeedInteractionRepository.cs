using Formax.Domain.Entities;

public interface IFeedInteractionRepository
{
    Task SaveAsync(FeedInteractionEvent entity);

    // 🔥 ESKİ (GERİ EKLENDİ)
    Task<int> CountByEventTypeAsync(FeedSignalType type);
    // 🔥 YENİ
    Task<double> GetCtrAsync(double minScore, double maxScore);
    Task<double> GetSkipRateAsync(double minScore, double maxScore);
    Task<List<ScoreBucketResult>> GetScoreBucketsAsync();
    Task<List<FeedInteractionEvent>> GetAllAsync();
}