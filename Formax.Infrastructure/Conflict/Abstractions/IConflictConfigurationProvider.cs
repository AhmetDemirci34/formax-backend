namespace Formax.Infrastructure.Conflict.Abstractions;

/// <summary>
/// Conflict yapılandırmasının KAYNAĞINI soyutlar. Engine bu soyutlamaya bağlıdır; somut kaynağa değil.
/// İleride AppSettings / Database / Admin Panel, aynı Engine'i DEĞİŞTİRMEDEN farklı bir sağlayıcıyla beslenir.
/// </summary>
public interface IConflictConfigurationProvider
{
    ConflictConfiguration Get();
}
