using Formax.Application.DTOs.Recommendations;
using Formax.Application.Interfaces.Discovery;

namespace Formax.Application.Services.Discovery;

/// <summary>
/// Discovery Engine orkestrasyonu — İLK FAZ.
///
/// Şu an düşük seviyeli Recommendation üreticisini (GetRecommendationFeedUseCase) kullanır.
/// Discovery Filter (yalnız Upcoming/Live), AiTrustScore ve Status/IsLive/LiveMinute alanları
/// bu üreticide zaten uygulanır ve DTO'ya taşınır.
///
/// KOMPOZİSYON SEAM'İ: Odds Intelligence, Prediction, Combo, Diversity, News, Injury,
/// Momentum, Personalization engine'leri veri kaynakları hazır olduğunda TAM BURADA
/// birleştirilecektir (her biri SRP; çıktıları RecommendationCardDto'nun ilgili alanlarına
/// yazılır). Recommendation Engine'e yeni kural EKLENMEZ.
///
/// Not: Odds/TopPrediction alanları şu an boştur — OddsSnapshot tablosunda gerçek maçlar için
/// veri yoktur (yalnız sentetik test satırları). Veri toplama katmanı geldiğinde bu engine
/// gerçek oranları buradan yazacaktır; hiçbir değer uydurulmaz.
/// </summary>
public sealed class DiscoveryEngine : IDiscoveryEngine
{
    private readonly GetRecommendationFeedUseCase _recommendationFeed;

    public DiscoveryEngine(GetRecommendationFeedUseCase recommendationFeed)
    {
        _recommendationFeed = recommendationFeed;
    }

    public Task<List<RecommendationCardDto>> BuildFeedAsync(
        int userId, int page, int pageSize, int? maxHorizonDays = null)
        // Düşük seviyeli öneri üreticisini orkestre et (Discovery Filter + AiTrust burada uygulanır).
        // Hero ufku aday üretiminde uygulanır; parametre verilmezse davranış değişmez.
        => _recommendationFeed.Execute(userId, page, pageSize, maxHorizonDays);
}
