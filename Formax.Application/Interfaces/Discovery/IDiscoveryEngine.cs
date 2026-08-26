using Formax.Application.DTOs.Recommendations;

namespace Formax.Application.Interfaces.Discovery;

/// <summary>
/// Discovery Engine — Discovery deneyiminin ORKESTRASYON katmanı (yeni mimari).
///
/// Recommendation Engine (düşük seviyeli öneri üreticisi) + gelecekteki tek-sorumluluklu
/// engine'lerin (AI Trust, Prediction, Odds Intelligence, News, Injury, Momentum, Combo,
/// Personalization, Diversity) çıktılarını birleştirerek Discovery bileşenleri (Hero, Feed,
/// Günün AI Kombini, Sana Özel) için nihai DTO'ları üretir.
///
/// Kurallar:
///  • Frontend yalnız bu DTO'ları render eder; hiçbir AI hesabı frontend'de yapılmaz.
///  • Her engine tek sorumluluğa sahiptir (SRP); hiçbir engine başkasının işini üstlenmez.
///  • Recommendation Engine'e YENİ kural eklenmez; yalnız burada orkestre edilir.
/// </summary>
public interface IDiscoveryEngine
{
    /// <summary>
    /// Feed/Hero için Discovery kartları (backend sıralı; yalnız Upcoming/Live).
    ///
    /// <paramref name="maxHorizonDays"/>: Hero/swipe kuyruğunun zaman ufku (bugün + N takvim
    /// günü). null = sınır yok. Discover Hero yakın maçların keşfidir; "Sana Özel", "Günün AI
    /// Kombini" ve Maçlar kendi geniş evrenlerini korur — bu yüzden ufuk global değil,
    /// yüzeye özel bir parametredir.
    /// </summary>
    Task<List<RecommendationCardDto>> BuildFeedAsync(
        int userId, int page, int pageSize, int? maxHorizonDays = null);
}
