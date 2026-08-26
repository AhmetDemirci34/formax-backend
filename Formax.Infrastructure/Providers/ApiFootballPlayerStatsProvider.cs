using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Application.Services.Players.Intelligence;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.Providers
{
    /// <summary>
    /// Oyuncu istatistik sağlayıcısı — MEVCUT api-football entegrasyonunun genişletilmişi.
    /// Aynı config (ApiFootball:ApiKey/BaseUrl), aynı header (x-apisports-key), aynı IMemoryCache
    /// deseni. Anahtar yoksa / oyuncu çözülemezse null → engine graceful fallback yapar.
    /// v1 kimlik: isimle çözer (players/profiles?search); stats: players?id=&season=.
    /// </summary>
    public sealed class ApiFootballPlayerStatsProvider : IPlayerStatsProvider
    {
        private const string DefaultBaseUrl = "https://v3.football.api-sports.io";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(6);

        private readonly HttpClient _http;
        private readonly IMemoryCache _cache;
        private readonly ILogger<ApiFootballPlayerStatsProvider> _logger;
        private readonly string _apiKey;

        public ApiFootballPlayerStatsProvider(
            HttpClient http, IMemoryCache cache, IConfiguration config,
            ILogger<ApiFootballPlayerStatsProvider> logger)
        {
            _http = http;
            _cache = cache;
            _logger = logger;
            _apiKey = config["ApiFootball:ApiKey"] ?? string.Empty;
            var baseUrl = config["ApiFootball:BaseUrl"] ?? DefaultBaseUrl;
            _http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            if (!string.IsNullOrWhiteSpace(_apiKey))
                _http.DefaultRequestHeaders.Add("x-apisports-key", _apiKey);
        }

        public bool IsEnabled => !string.IsNullOrWhiteSpace(_apiKey);

        public async Task<PlayerStats?> GetPlayerStatsAsync(string playerName, int teamId, CancellationToken ct = default)
        {
            if (!IsEnabled || string.IsNullOrWhiteSpace(playerName)) return null;

            var cacheKey = $"apifootball-player:{playerName.ToLowerInvariant()}";
            if (_cache.TryGetValue(cacheKey, out PlayerStats? cached)) return cached;

            PlayerStats? result = null;
            try
            {
                var search = LastName(playerName);
                if (search.Length < 4) search = playerName.Trim();

                var profile = await _http.GetFromJsonAsync<ProfilesResponse>(
                    $"players/profiles?search={Uri.EscapeDataString(search)}", ct);

                var pid = ResolveId(profile, playerName);
                if (pid is int id)
                {
                    var season = CurrentSeason();
                    var statsResp = await _http.GetFromJsonAsync<PlayersResponse>(
                        $"players?id={id}&season={season}", ct);
                    result = MapStats(playerName, id, statsResp);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[API-FOOTBALL/PLAYER] '{Name}' çözülemedi (graceful)", playerName);
            }

            result ??= new PlayerStats { PlayerName = playerName, Found = false };
            _cache.Set(cacheKey, result, CacheTtl);
            return result;
        }

        // profiles yanıtından en iyi isim eşleşmesinin oyuncu id'si.
        private static int? ResolveId(ProfilesResponse? profile, string playerName)
        {
            var items = profile?.Response;
            if (items is null || items.Count == 0) return null;

            var last = LastName(playerName).ToLowerInvariant();
            var match = items.FirstOrDefault(i =>
                (i.Player?.Name ?? "").ToLowerInvariant().Contains(last)) ?? items[0];
            return match.Player?.Id;
        }

        private static PlayerStats MapStats(string name, int pid, PlayersResponse? resp)
        {
            var lines = resp?.Response is { Count: > 0 } ? resp.Response[0].Statistics : null;
            if (lines is null || lines.Count == 0)
                return new PlayerStats { PlayerName = name, ExternalPlayerId = pid, Found = false };

            int goals = 0, assists = 0, minutes = 0, apps = 0, ratingCount = 0;
            double ratingSum = 0;
            foreach (var l in lines)
            {
                goals += l.Goals?.Total ?? 0;
                assists += l.Goals?.Assists ?? 0;
                minutes += l.Games?.Minutes ?? 0;
                apps += l.Games?.Appearences ?? 0;
                if (double.TryParse(l.Games?.Rating, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var r) && r > 0)
                { ratingSum += r; ratingCount++; }
            }

            return new PlayerStats
            {
                PlayerName = name,
                ExternalPlayerId = pid,
                Rating = ratingCount > 0 ? ratingSum / ratingCount : null,
                FormRating = null,
                Goals = goals,
                Assists = assists,
                Minutes = minutes,
                AppearanceCount = apps,
                Found = true,
            };
        }

        private static int CurrentSeason()
        {
            var now = DateTime.UtcNow;
            return now.Month >= 7 ? now.Year : now.Year - 1;
        }

        private static string LastName(string name)
        {
            var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? "" : parts[^1];
        }

        // ── Minimal api-football yanıt modelleri (yalnız okunan alanlar) ─────────
        private sealed class ProfilesResponse
        {
            [JsonPropertyName("response")] public List<ProfileItem>? Response { get; set; }
        }
        private sealed class ProfileItem
        {
            [JsonPropertyName("player")] public PlayerCore? Player { get; set; }
        }
        private sealed class PlayerCore
        {
            [JsonPropertyName("id")] public int? Id { get; set; }
            [JsonPropertyName("name")] public string? Name { get; set; }
        }
        private sealed class PlayersResponse
        {
            [JsonPropertyName("response")] public List<PlayerEntry>? Response { get; set; }
        }
        private sealed class PlayerEntry
        {
            [JsonPropertyName("statistics")] public List<StatLine>? Statistics { get; set; }
        }
        private sealed class StatLine
        {
            [JsonPropertyName("games")] public GamesBlock? Games { get; set; }
            [JsonPropertyName("goals")] public GoalsBlock? Goals { get; set; }
        }
        private sealed class GamesBlock
        {
            [JsonPropertyName("appearences")] public int? Appearences { get; set; }
            [JsonPropertyName("minutes")] public int? Minutes { get; set; }
            [JsonPropertyName("rating")] public string? Rating { get; set; }
        }
        private sealed class GoalsBlock
        {
            [JsonPropertyName("total")] public int? Total { get; set; }
            [JsonPropertyName("assists")] public int? Assists { get; set; }
        }
    }
}
