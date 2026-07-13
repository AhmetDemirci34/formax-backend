using Formax.Infrastructure.Merge;
using Formax.Infrastructure.Normalize.Models;

namespace Formax.Infrastructure.Persistence;

/// <summary>
/// Weather kalıcılaştırma isteği: (opsiyonel) FORMAX Match ID + son <see cref="MergeResult{T}"/>.
/// Weather koordinat-eksenlidir; <see cref="FormaxMatchId"/> yoksa (standalone) null taşınır.
/// </summary>
public sealed record GdpWeatherPersistRequest
{
    /// <summary>İlişkili maç (varsa).</summary>
    public string? FormaxMatchId { get; init; }

    /// <summary>
    /// Kanonik (istenen) koordinat — gerçek Match'ten çözülen; provider'ın döndürdüğü grid değeri DEĞİL.
    /// Persist satırının Latitude/Longitude'u buradan yazılır (çok-provider merge'inde koordinat çakışmaz).
    /// </summary>
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }

    /// <summary>Son merge sonucu (Value + Fields/durumlar + ConflictResolutions) — ölçümler (sıcaklık/durum/tahmin).</summary>
    public required MergeResult<NormalizedWeather> Merge { get; init; }
}

/// <summary>Bir weather kaydının kalıcılaştırma sonucu (durum + varsa hata).</summary>
public sealed record GdpWeatherPersistOutcome
{
    public string? FormaxMatchId { get; init; }
    public required GdpPersistStatus Status { get; init; }
    public string? Error { get; init; }
}
