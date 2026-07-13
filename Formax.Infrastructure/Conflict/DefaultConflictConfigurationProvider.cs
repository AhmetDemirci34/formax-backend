using Formax.Infrastructure.Conflict.Abstractions;

namespace Formax.Infrastructure.Conflict;

/// <summary>
/// Varsayılan (sabit) yapılandırma sağlayıcısı. Kod içi varsayılan <see cref="ConflictConfiguration"/>'ı döndürür.
/// İleride AppSettings/DB/Panel sağlayıcıları bu arayüzü farklı uygulayarak Engine'i değiştirmeden devreye girer.
/// </summary>
public sealed class DefaultConflictConfigurationProvider : IConflictConfigurationProvider
{
    private readonly ConflictConfiguration _configuration = new();

    public ConflictConfiguration Get() => _configuration;
}
