namespace Formax.Infrastructure.Providers.Abstractions;

/// <summary>
/// Sağlayıcının erişim/maliyet sınıfı.
///
/// FORMAX ÇEKİRDEK KURALI: sistem yalnızca <see cref="Free"/> sağlayıcılarla tam çalışabilmelidir.
/// <see cref="Freemium"/> ve <see cref="Paid"/> desteklenir ama HER ZAMAN opsiyoneldir;
/// hiçbir zaman sistemin çalışması için zorunlu değildir. Varsayılan sınıf <see cref="Free"/>'dir.
/// </summary>
public enum ProviderCategory
{
    Free = 0,
    Freemium = 1,
    Paid = 2
}
