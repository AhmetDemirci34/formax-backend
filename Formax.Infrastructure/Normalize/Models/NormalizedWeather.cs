using System;

namespace Formax.Infrastructure.Normalize.Models;

/// <summary>
/// Ortak hava durumu modeli. Koordinat eksenlidir (maç kimliğine kuplaj YOK — weather standalone).
/// Zaman her zaman UTC olarak taşınır; sıcaklık °C. Eksik alanlar null bırakılır (sahte veri üretilmez).
/// Canonical Domain <see cref="Formax.Domain.Entities.MatchWeather"/> ile birebir hizalıdır.
/// </summary>
public sealed record NormalizedWeather
{
    /// <summary>Çözünmüş enlem.</summary>
    public double? Latitude { get; init; }

    /// <summary>Çözünmüş boylam.</summary>
    public double? Longitude { get; init; }

    /// <summary>Sıcaklık (°C).</summary>
    public double? TemperatureC { get; init; }

    /// <summary>Ortak hava durumu metni (ör. "Clear", "Rain", "Snow").</summary>
    public string? Condition { get; init; }

    /// <summary>Tahminin geçerli olduğu an (UTC).</summary>
    public DateTimeOffset? ForecastUtc { get; init; }
}
