using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.Fixtures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.Fixtures.Providers
{
    /// <summary>
    /// FORMAX Data Engine v1 — TheSportsDB açık fixture provider'ı (ücretsiz).
    /// Gün bazlı eventsday endpoint'inden futbol maçlarını çeker. Anahtar yapılandırma
    /// ile değişebilir; varsayılan ücretsiz test anahtarı "3". Hata → boş liste.
    /// </summary>
    public sealed class TheSportsDbFixtureProvider : IFixtureProvider
    {
        private const int MaxDays = 31;

        private readonly HttpClient _http;
        private readonly ILogger<TheSportsDbFixtureProvider> _logger;
        private readonly string _key;

        public TheSportsDbFixtureProvider(HttpClient http, IConfiguration config,
            ILogger<TheSportsDbFixtureProvider> logger)
        {
            _http = http;
            _logger = logger;
            _key = config["Fixtures:TheSportsDb:Key"] ?? "3";
            if (_http.Timeout == default || _http.Timeout.TotalSeconds > 20)
                _http.Timeout = TimeSpan.FromSeconds(15);
        }

        public string Name => "TheSportsDB";
        public bool IsEnabled => !string.IsNullOrWhiteSpace(_key);

        public async Task<IReadOnlyList<FixtureCandidate>> DiscoverAsync(
            DateOnly fromUtc, DateOnly toUtc, CancellationToken ct = default)
        {
            var results = new List<FixtureCandidate>();
            var day = fromUtc;
            var guard = 0;

            while (day <= toUtc && guard++ < MaxDays)
            {
                ct.ThrowIfCancellationRequested();
                var url = $"https://www.thesportsdb.com/api/v1/json/{_key}/eventsday.php?d={day:yyyy-MM-dd}&s=Soccer";
                try
                {
                    var json = await _http.GetStringAsync(url, ct);
                    Parse(json, results);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[FIXTURE/TSDB] gün {Day} alınamadı", day);
                }
                day = day.AddDays(1);
            }

            return results;
        }

        private void Parse(string json, List<FixtureCandidate> sink)
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("events", out var events) ||
                events.ValueKind != JsonValueKind.Array)
                return;

            foreach (var e in events.EnumerateArray())
            {
                var home = Str(e, "strHomeTeam");
                var away = Str(e, "strAwayTeam");
                if (string.IsNullOrWhiteSpace(home) || string.IsNullOrWhiteSpace(away)) continue;

                var ts = Str(e, "strTimestamp");
                if (!DateTime.TryParse(ts, null,
                        System.Globalization.DateTimeStyles.AdjustToUniversal |
                        System.Globalization.DateTimeStyles.AssumeUniversal, out var dateUtc))
                {
                    var d = Str(e, "dateEvent");
                    if (!DateTime.TryParse(d, out dateUtc)) continue;
                    dateUtc = DateTime.SpecifyKind(dateUtc, DateTimeKind.Utc);
                }

                sink.Add(new FixtureCandidate
                {
                    League = Str(e, "strLeague"),
                    Country = Str(e, "strCountry"),
                    Season = Str(e, "strSeason"),
                    Round = NullIfEmpty(Str(e, "intRound")),
                    DateUtc = dateUtc.ToUniversalTime(),
                    HomeTeam = home,
                    AwayTeam = away,
                    Venue = NullIfEmpty(Str(e, "strVenue")),
                    Status = MapStatus(Str(e, "strStatus")),
                    Source = Name,
                    SourceConfidence = 75,
                    ProviderPriority = 2
                });
            }
        }

        private static string MapStatus(string raw) => (raw ?? "").Trim().ToUpperInvariant() switch
        {
            "MATCH FINISHED" or "FT" or "AET" or "PEN" => "Finished",
            "1H" or "2H" or "HT" or "LIVE" or "ET" => "Live",
            "" or "NS" or "NOT STARTED" => "NotStarted",
            _ => "NotStarted"
        };

        private static string Str(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString() ?? "" : "";

        private static string? NullIfEmpty(string s) =>
            string.IsNullOrWhiteSpace(s) || s == "0" ? null : s;
    }
}
