using System;

namespace Formax.Infrastructure.Providers.Abstractions;

/// <summary>
/// Sağlayıcılara verilen nötr sorgu.
/// FORMAX_MATCH_ID ekseninde ya da global keşif (match id yok) modunda çalışabilir.
/// Bu istek, Normalize / Merge katmanlarından bağımsızdır.
/// </summary>
public sealed record ProviderRequest
{
    /// <summary>Belirli bir maç için sorgu. Global keşifte null olabilir.</summary>
    public string? FormaxMatchId { get; init; }

    /// <summary>İsteğe bağlı zaman aralığı (UTC) başlangıcı.</summary>
    public DateTimeOffset? FromUtc { get; init; }

    /// <summary>İsteğe bağlı zaman aralığı (UTC) bitişi.</summary>
    public DateTimeOffset? ToUtc { get; init; }

    // ---- Match Context (gerçek Match'ten çözümlenir; sağlayıcılar sabit değer KULLANMAZ) ----

    /// <summary>Ev sahibi takım adı (gerçek Match'ten).</summary>
    public string? HomeTeam { get; init; }

    /// <summary>Deplasman takım adı (gerçek Match'ten).</summary>
    public string? AwayTeam { get; init; }

    /// <summary>Turnuva/lig adı (gerçek Match'ten).</summary>
    public string? Competition { get; init; }

    /// <summary>
    /// Seçilen maçın sağlayıcı fikstür kimliği (canonical <c>Match.ExternalMatchId</c>).
    /// MATCH-SCOPE anahtarıdır: tarih-bazlı sağlayıcılar (ör. api-football <c>/fixtures?date=</c>)
    /// günün TÜM fikstürlerini döndürür; bu alan doluyken sağlayıcı sonucu yalnız bu fikstüre daraltır.
    /// Global keşif modunda (match id yok) null'dır ve hiçbir daraltma uygulanmaz.
    /// </summary>
    public string? ExternalMatchId { get; init; }

    /// <summary>Çözümlenen mekan adı (geocode display_name); yoksa null.</summary>
    public string? VenueName { get; init; }

    /// <summary>Çözümlenen enlem (ev sahibi konumundan geocode); çözülemezse null.</summary>
    public double? Latitude { get; init; }

    /// <summary>Çözümlenen boylam; çözülemezse null.</summary>
    public double? Longitude { get; init; }
}
