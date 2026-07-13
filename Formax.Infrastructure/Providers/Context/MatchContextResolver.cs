using System;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Providers.Abstractions;
using Formax.Infrastructure.Providers.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.Providers.Context;

/// <summary>
/// Gerçek Domain Match'i sağlayıcı bağlamına çözer. Takım adları ve turnuva doğrudan Match'ten;
/// koordinat, ev sahibi takım adının OSM/Nominatim ile geocode edilmesinden gelir (Match/Team modelinde
/// konum verisi yoktur — tek gerçek sinyal takım adıdır). Geocode başarısızsa koordinat null bırakılır;
/// sabit/uydurma koordinat ASLA üretilmez.
/// </summary>
public sealed class MatchContextResolver : IMatchContextResolver
{
    private readonly FormaxDbContext _db;
    private readonly HttpClient _http;
    private readonly IProviderConfigurationService _configuration;
    private readonly ILogger<MatchContextResolver> _logger;

    public MatchContextResolver(
        FormaxDbContext db,
        HttpClient http,
        IProviderConfigurationService configuration,
        ILogger<MatchContextResolver> logger)
    {
        _db = db;
        _http = http;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ProviderRequest> ResolveAsync(int matchId, CancellationToken cancellationToken = default)
    {
        var match = await _db.Matches
            .AsNoTracking()
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .FirstOrDefaultAsync(m => m.Id == matchId, cancellationToken)
            .ConfigureAwait(false);

        if (match is null)
        {
            _logger.LogWarning("MatchContextResolver: Match {MatchId} bulunamadı; boş istek döndürülüyor.", matchId);
            return new ProviderRequest();
        }

        var homeTeam = NullIfBlank(match.HomeTeam?.Name);
        var awayTeam = NullIfBlank(match.AwayTeam?.Name);
        var competition = NullIfBlank(match.League);
        var formaxMatchId = $"fx-match-{match.Id}";

        var (latitude, longitude, venueName) = await GeocodeAsync(homeTeam, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "MatchContextResolver: match={MatchId} formaxId={FormaxId} home={Home} away={Away} competition={Competition} coords=({Lat},{Lon}) venue={Venue}",
            matchId, formaxMatchId, homeTeam, awayTeam, competition, latitude, longitude, venueName);

        return new ProviderRequest
        {
            FormaxMatchId = formaxMatchId,
            HomeTeam = homeTeam,
            AwayTeam = awayTeam,
            Competition = competition,
            VenueName = venueName,
            Latitude = latitude,
            Longitude = longitude,
            FromUtc = new DateTimeOffset(DateTime.SpecifyKind(match.MatchDate, DateTimeKind.Utc), TimeSpan.Zero)
        };
    }

    /// <summary>
    /// Ev sahibi takım adını OSM/Nominatim ile geocode eder (base URL merkezi config'ten; sabit URL yok).
    /// Başarısız/boş sonuçta (null, null, null) döner — sahte koordinat üretmez.
    /// </summary>
    private async Task<(double? Lat, double? Lon, string? Venue)> GeocodeAsync(string? homeTeam, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(homeTeam))
            return (null, null, null);

        var config = _configuration.Get("openstreetmap", ProviderCapability.Venue);
        if (config is null || string.IsNullOrWhiteSpace(config.BaseUrl))
        {
            _logger.LogWarning("MatchContextResolver: OSM/Nominatim yapılandırması (BaseUrl) yok; koordinat çözülemedi.");
            return (null, null, null);
        }

        var url = $"{config.BaseUrl}?q={Uri.EscapeDataString(homeTeam)}&format=json&limit=1";

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (config.Timeout > TimeSpan.Zero)
                timeoutCts.CancelAfter(config.Timeout);

            using var message = new HttpRequestMessage(HttpMethod.Get, url);
            // Nominatim kullanım politikası: tanımlı User-Agent zorunlu.
            message.Headers.TryAddWithoutValidation("User-Agent", "FORMAX-GDP/1.0 (+https://formax)");
            message.Headers.TryAddWithoutValidation("Accept", "application/json");

            using var response = await _http.SendAsync(message, timeoutCts.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("MatchContextResolver: geocode HTTP {Status} — home={Home}", (int)response.StatusCode, homeTeam);
                return (null, null, null);
            }

            var body = await response.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
            {
                _logger.LogInformation("MatchContextResolver: geocode sonuç yok — home={Home} (koordinatsız devam).", homeTeam);
                return (null, null, null);
            }

            var first = doc.RootElement[0];
            var lat = ParseCoordinate(first, "lat");
            var lon = ParseCoordinate(first, "lon");
            var venue = first.TryGetProperty("display_name", out var dn) && dn.ValueKind == JsonValueKind.String
                ? dn.GetString()
                : null;

            if (lat is null || lon is null)
                return (null, null, null);

            return (lat, lon, venue);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MatchContextResolver: geocode başarısız — home={Home} (koordinatsız devam).", homeTeam);
            return (null, null, null);
        }
    }

    private static double? ParseCoordinate(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
            return null;

        var text = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };

        return !string.IsNullOrWhiteSpace(text)
               && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : (double?)null;
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
