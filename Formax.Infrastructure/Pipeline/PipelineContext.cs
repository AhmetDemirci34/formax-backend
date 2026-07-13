using System;
using System.Collections.Generic;

namespace Formax.Infrastructure.Pipeline;

/// <summary>
/// Aşamalar arasında taşınan ortak bağlam.
/// Her aşama çıktısını buraya yazabilir, sonraki aşama okuyabilir.
/// Tür-bağımsız bir anahtar/değer torbasıdır; hiçbir engine sonuç tipine kuplaj kurmaz.
/// </summary>
public sealed class PipelineContext
{
    private readonly Dictionary<string, object?> _items = new(StringComparer.Ordinal);

    /// <summary>İşlenen maçın FORMAX Match ID'si (string; MatchIdentity'ye kuplaj yok).</summary>
    public string? FormaxMatchId { get; init; }

    /// <summary>
    /// Pipeline'ı süren gerçek Domain Match'in Id'si (varsa). Match Context Resolver bunu
    /// gerçek takım/lig/koordinata çözer; sağlayıcılar sabit değer kullanmaz.
    /// </summary>
    public int? MatchId { get; init; }

    /// <summary>Aşamaların yazdığı çıktı torbası (salt-okunur görünüm).</summary>
    public IReadOnlyDictionary<string, object?> Items => _items;

    /// <summary>Bir çıktıyı bağlama yazar (varsa üzerine yazar).</summary>
    public void Set(string key, object? value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Anahtar boş olamaz.", nameof(key));

        _items[key] = value;
    }

    /// <summary>Bir çıktıyı tip güvenli okur.</summary>
    public bool TryGet<T>(string key, out T? value)
    {
        value = default;
        if (!string.IsNullOrWhiteSpace(key) && _items.TryGetValue(key, out var raw) && raw is T typed)
        {
            value = typed;
            return true;
        }

        return false;
    }

    public T? Get<T>(string key) => TryGet<T>(key, out var value) ? value : default;
}
