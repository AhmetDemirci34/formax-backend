namespace Formax.Infrastructure.Providers.Abstractions;

/// <summary>
/// Tüm GDP sağlayıcılarının ortak kimliği.
/// Her sağlayıcı kendini tek bir <see cref="ProviderManifest"/> ile tanımlar;
/// Registry keşif/filtre, Orchestrator sıralama ve yetenek-bazlı seçim için bunu okur.
/// </summary>
public interface IDataProvider
{
    ProviderManifest Manifest { get; }
}
