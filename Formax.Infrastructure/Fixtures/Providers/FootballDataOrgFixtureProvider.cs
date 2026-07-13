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
    /// FORMAX Data Engine v1 — Football-Data.org açık fixture provider'ı (ücretsiz tier).
    /// Ücretsiz erişim için bir API token gerekir (Fixtures:FootballDataOrg:Token).
    /// Token yoksa provider otomatik DEVRE DIŞI olur (IsEnabled=false) → motor onsuz çalışır.
    /// </summary>
    public sealed class FootballDataOrgFixtureProvider : IFixtureProvider
    {
        private readonly HttpClient _http;
        private readonly ILogger<FootballDataOrgFixtureProvider> _logger;
        private readonly string? _token;

        public FootballDataOrgFixtureProvider(HttpClient http, IConfiguration config,
            ILogger<FootballDataOrgFixtureProvider> logger)
        {
            _http = http;
            _logger = logger;
            _token = config["Fixtures:FootballDataOrg:Token"];
            if (_http.Timeout == default || _http.Timeout.TotalSeconds > 20)
                _http.Timeout = TimeSpan.FromSeconds(15);
        }

        public string Name => "Football-Data.org";
        public bool IsEnabled => !string.IsNullOrWhiteSpace(_token);

        public async Task<IReadOnlyList<FixtureCandidate>> DiscoverAsync(
            DateOnly fromUtc, DateOnly toUtc, CancellationToken ct = default)
        {
            var results = new List<FixtureCandidate>();
            if (!IsEnabled) return results;

            var url = $"https://api.football-data.org/v4/matches?dateFrom={fromUtc:yyyy-MM-dd}&dateTo={toUtc:yyyy-MM-dd}";
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Add("X-Auth-Token", _token);
                using var resp = await _http.SendAsync(req, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("[FIXTURE/FDO] HTTP {Status}", resp.StatusCode);
                    return results;
                }
                Parse(await resp.Content.ReadAsStringAsync(ct), results);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[FIXTURE/FDO] istek başarısız");
            }

            return results;
        }

        private void Parse(string json, List<FixtureCandidate> sink)
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("matches", out var matches) ||
                matches.ValueKind != JsonValueKind.Array)
                return;

            foreach (var m in matches.EnumerateArray())
            {
                var home = Nested(m, "homeTeam", "name");
                var away = Nested(m, "awayTeam", "name");
                if (string.IsNullOrWhiteSpace(home) || string.IsNullOrWhiteSpace(away)) continue;

                if (!DateTime.TryParse(Str(m, "utcDate"), null,
                        System.Globalization.DateTimeStyles.AdjustToUniversal |
                        System.Globalization.DateTimeStyles.AssumeUniversal, out var dateUtc))
                    continue;

                sink.Add(new FixtureCandidate
                {
                    League = Nested(m, "competition", "name"),
                    Country = Nested(m, "area", "name"),
                    Season = "",
                    Round = m.TryGetProperty("matchday", out var md) && md.ValueKind == JsonValueKind.Number
                        ? md.GetInt32().ToString() : null,
                    DateUtc = dateUtc.ToUniversalTime(),
                    HomeTeam = home,
                    AwayTeam = away,
                    Status = MapStatus(Str(m, "status")),
                    Source = Name,
                    SourceConfidence = 85,
                    ProviderPriority = 3
                });
            }
        }

        private static string MapStatus(string raw) => (raw ?? "").Trim().ToUpperInvariant() switch
        {
            "FINISHED" or "AWARDED" => "Finished",
            "IN_PLAY" or "PAUSED" or "LIVE" => "Live",
            _ => "NotStarted"
        };

        private static string Str(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString() ?? "" : "";

        private static string Nested(JsonElement el, string obj, string prop) =>
            el.TryGetProperty(obj, out var o) && o.ValueKind == JsonValueKind.Object
                ? Str(o, prop) : "";
    }
}
