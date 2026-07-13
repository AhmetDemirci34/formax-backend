namespace Formax.Infrastructure.Providers.Health;

/// <summary>
/// Bir sağlayıcı+yetenek çiftinin sağlık durumu.
/// Not: Bu duruma hangi metriklerin hangi eşiklerle karar vereceği (health algoritması) bu fazda
/// YAZILMAZ; sınıflandırma sonraki fazda uygulanır. Varsayılan <see cref="Offline"/>.
/// </summary>
public enum ProviderHealthStatus
{
    Healthy = 0,
    Warning = 1,
    Critical = 2,
    Offline = 3
}
