using System;

namespace Formax.Infrastructure.MatchIdentity;

/// <summary>
/// Bir provider'ın tek bir maça dair kimlik referansı.
/// Bilerek provider-bağımsızdır: yalnızca primitive tanımlayıcılar taşır (Normalize modellerine kuplaj yok).
/// Tanımlayıcı alanlar, ileride geliştirilecek eşleştirme stratejilerinin girdisidir.
/// </summary>
public sealed record ProviderMatchReference
{
    /// <summary>Referansı üreten sağlayıcının adı.</summary>
    public required string ProviderName { get; init; }

    /// <summary>Sağlayıcının kendi maç kimliği (ham).</summary>
    public required string ProviderMatchId { get; init; }

    // ── Eşleştirme stratejileri için tanımlayıcı ipuçları (hepsi opsiyonel) ──

    /// <summary>Ev sahibi takım adı (Takımlar stratejisi).</summary>
    public string? HomeTeam { get; init; }

    /// <summary>Deplasman takım adı (Takımlar stratejisi).</summary>
    public string? AwayTeam { get; init; }

    /// <summary>Başlama zamanı, UTC (Başlama zamanı stratejisi).</summary>
    public DateTimeOffset? KickoffUtc { get; init; }

    /// <summary>Organizasyon/lig adı (Lig stratejisi).</summary>
    public string? Competition { get; init; }

    /// <summary>Ülke adı (lig ayrıştırmasına yardımcı).</summary>
    public string? Country { get; init; }
}
