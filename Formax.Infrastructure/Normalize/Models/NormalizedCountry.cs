namespace Formax.Infrastructure.Normalize.Models;

/// <summary>
/// Ortak ülke modeli. Sağlayıcıya özgü ülke adları/kodları buraya normalize edilir.
/// Kanonik ad eşleme ve ISO kod çözümü sonraki fazlarda genişletilecek.
/// </summary>
public sealed record NormalizedCountry
{
    /// <summary>Kanonik ülke adı (temizlenmiş).</summary>
    public required string Name { get; init; }

    /// <summary>ISO-3166 kodu (opsiyonel; iskelette null).</summary>
    public string? Code { get; init; }
}
