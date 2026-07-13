using System;
using System.Threading;
using System.Threading.Tasks;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.Persistence;

/// <summary>
/// GDP weather kalıcılaştırıcısı (EF Core) — Canonical Domain <see cref="MatchWeather"/>.
///
/// Kayıt anahtarı (FormaxMatchId + kanonik Latitude + Longitude)'dir. Kanonik koordinat GERÇEK maçtan
/// çözülen (istenen) koordinattır — provider'ın döndürdüğü grid değeri değil — böylece çok-provider
/// birleştirmede koordinat çakışmaz. Ölçümler (sıcaklık/durum/tahmin) merge sonucundan gelir.
/// Aynı anahtar için mevcut satır güncellenir; değişiklik yoksa Unchanged (idempotent).
/// </summary>
public sealed class GdpWeatherPersister : IGdpWeatherPersister
{
    private readonly FormaxDbContext _db;
    private readonly ILogger<GdpWeatherPersister> _logger;

    public GdpWeatherPersister(FormaxDbContext db, ILogger<GdpWeatherPersister> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<GdpWeatherPersistOutcome> PersistAsync(GdpWeatherPersistRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));

        var weather = request.Merge?.Value;
        if (weather is null)
            return Fail(request.FormaxMatchId, "Merge sonucu yok; MatchWeather beslenemez.");

        // Kanonik koordinat GERÇEK maçtan gelir. Geocode başarısızsa koordinat null → sahte veri yazmadan çık.
        if (request.Latitude is null || request.Longitude is null)
        {
            _logger.LogInformation(
                "GdpWeatherPersister: kanonik koordinat yok (matchId={MatchId}); MatchWeather yazılmadı (graceful skip).",
                request.FormaxMatchId);
            return Fail(request.FormaxMatchId, "Kanonik koordinat yok (geocode başarısız).");
        }

        var matchId = string.IsNullOrWhiteSpace(request.FormaxMatchId) ? null : request.FormaxMatchId;
        var lat = request.Latitude.Value;
        var lon = request.Longitude.Value;

        try
        {
            var existing = await _db.MatchWeathers
                .FirstOrDefaultAsync(
                    m => m.FormaxMatchId == matchId && m.Latitude == lat && m.Longitude == lon,
                    cancellationToken)
                .ConfigureAwait(false);

            if (existing is null)
            {
                _db.MatchWeathers.Add(new MatchWeather
                {
                    FormaxMatchId = matchId,
                    Latitude = lat,
                    Longitude = lon,
                    TemperatureC = weather.TemperatureC,
                    Condition = weather.Condition,
                    ForecastUtc = weather.ForecastUtc,
                    LastUpdatedUtc = DateTime.UtcNow
                });

                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation(
                    "GdpWeatherPersister: Inserted matchId={MatchId} coords=({Lat},{Lon}) tempC={Temp} cond={Cond}",
                    matchId, lat, lon, weather.TemperatureC, weather.Condition);
                return Outcome(matchId, GdpPersistStatus.Inserted);
            }

            var changed = false;
            if (existing.TemperatureC != weather.TemperatureC) { existing.TemperatureC = weather.TemperatureC; changed = true; }
            if (!string.Equals(existing.Condition, weather.Condition, StringComparison.Ordinal)) { existing.Condition = weather.Condition; changed = true; }
            if (existing.ForecastUtc != weather.ForecastUtc) { existing.ForecastUtc = weather.ForecastUtc; changed = true; }

            if (!changed)
            {
                _logger.LogInformation("GdpWeatherPersister: Unchanged matchId={MatchId} coords=({Lat},{Lon})", matchId, lat, lon);
                return Outcome(matchId, GdpPersistStatus.Unchanged);
            }

            existing.LastUpdatedUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(
                "GdpWeatherPersister: Updated matchId={MatchId} coords=({Lat},{Lon}) tempC={Temp} cond={Cond}",
                matchId, lat, lon, weather.TemperatureC, weather.Condition);
            return Outcome(matchId, GdpPersistStatus.Updated);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GdpWeatherPersister: persist hatası matchId={MatchId}", matchId);
            return Fail(matchId, ex.Message);
        }
    }

    private static GdpWeatherPersistOutcome Outcome(string? id, GdpPersistStatus status) =>
        new() { FormaxMatchId = id, Status = status };

    private static GdpWeatherPersistOutcome Fail(string? id, string error) =>
        new() { FormaxMatchId = id, Status = GdpPersistStatus.Failed, Error = error };
}
