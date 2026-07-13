using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Formax.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Formax.API.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/apifootball")]
    public class AdminApiFootballMappingController : ControllerBase
    {
        private readonly FormaxDbContext _db;
        private readonly IHttpClientFactory _http;
        private readonly IConfiguration _config;

        public AdminApiFootballMappingController(
            FormaxDbContext db,
            IHttpClientFactory http,
            IConfiguration config)
        {
            _db = db;
            _http = http;
            _config = config;
        }

        /// <summary>
        /// Tüm FORMAX takımlarını API-Football ile eşleştirir.
        /// leagueId: API-Football lig ID (default 203 = Türkiye Süper Lig)
        /// season:   Sezon yılı (default mevcut yıl)
        /// dryRun:   true → DB'ye yazmaz, sadece preview döner
        /// </summary>
        [HttpPost("bootstrap-team-mapping")]
        public async Task<IActionResult> BootstrapTeamMapping(
            [FromQuery] int leagueId = 203,
            [FromQuery] int? season = null,
            [FromQuery] bool dryRun = true)
        {
            var apiKey = _config["ApiFootball:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
                return BadRequest("ApiFootball:ApiKey config boş. appsettings.json içine API key ekleyin.");

            var effectiveSeason = season ?? DateTime.UtcNow.Year;

            // 1 — API-Football'dan lig takımlarını çek (1 quota)
            var afTeams = await FetchApiFootballTeams(apiKey, leagueId, effectiveSeason);
            if (afTeams == null)
                return StatusCode(502, "API-Football yanıt vermedi veya parse hatası.");

            // 2 — FORMAX takımlarını çek
            var formaxTeams = await _db.Teams.ToListAsync();

            // 3 — Eşleştir
            var matched = new List<MappingResult>();
            var unmatched = new List<MappingResult>();

            foreach (var ft in formaxTeams)
            {
                var normalizedFormax = Normalize(ft.Name);
                var best = FindBest(normalizedFormax, afTeams);

                if (best != null)
                {
                    matched.Add(new MappingResult
                    {
                        FormaxTeamId = ft.Id,
                        FormaxName = ft.Name,
                        ApiFootballTeamId = best.TeamId,
                        ApiFootballName = best.TeamName,
                        MatchMethod = best.Method
                    });

                    if (!dryRun)
                    {
                        ft.ApiFootballTeamId = best.TeamId;
                        _db.Teams.Update(ft);
                    }
                }
                else
                {
                    unmatched.Add(new MappingResult
                    {
                        FormaxTeamId = ft.Id,
                        FormaxName = ft.Name,
                        ApiFootballTeamId = null,
                        ApiFootballName = null,
                        MatchMethod = "NO_MATCH"
                    });
                }
            }

            if (!dryRun)
                await _db.SaveChangesAsync();

            return Ok(new
            {
                dryRun,
                leagueId,
                season = effectiveSeason,
                quotaUsed = 1,
                totalFormaxTeams = formaxTeams.Count,
                matchedCount = matched.Count,
                unmatchedCount = unmatched.Count,
                matchedTeams = matched,
                unmatchedTeams = unmatched,
                note = dryRun
                    ? "DRY RUN: DB'ye yazılmadı. dryRun=false ile tekrar çağırın."
                    : "Mapping tamamlandı. DB güncellendi."
            });
        }

        private async Task<List<AfTeam>?> FetchApiFootballTeams(string apiKey, int leagueId, int season)
        {
            using var client = _http.CreateClient("ApiFootball");
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("x-apisports-key", apiKey);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var baseUrl = _config["ApiFootball:BaseUrl"] ?? "https://v3.football.api-sports.io";
            var url = $"{baseUrl}/teams?league={leagueId}&season={season}";

            HttpResponseMessage response;
            try
            {
                response = await client.GetAsync(url);
            }
            catch
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();

            try
            {
                var doc = JsonDocument.Parse(json);
                var results = doc.RootElement.GetProperty("response");
                var teams = new List<AfTeam>();

                foreach (var item in results.EnumerateArray())
                {
                    var teamEl = item.GetProperty("team");
                    var id = teamEl.GetProperty("id").GetInt32();
                    var name = teamEl.GetProperty("name").GetString() ?? "";
                    var country = teamEl.TryGetProperty("country", out var c) ? c.GetString() ?? "" : "";
                    teams.Add(new AfTeam(id, name, country));
                }

                return teams;
            }
            catch
            {
                return null;
            }
        }

        private static BestMatch? FindBest(string normalizedFormax, List<AfTeam> afTeams)
        {
            // Adım 1: Tam eşleşme (normalize edilmiş)
            foreach (var af in afTeams)
            {
                if (Normalize(af.TeamName) == normalizedFormax)
                    return new BestMatch(af.TeamId, af.TeamName, "EXACT");
            }

            // Adım 2: Contains eşleşmesi (normalFormax, af adı içinde geçiyor ya da tersi)
            foreach (var af in afTeams)
            {
                var normalAf = Normalize(af.TeamName);
                if (normalAf.Contains(normalizedFormax) || normalizedFormax.Contains(normalAf))
                    return new BestMatch(af.TeamId, af.TeamName, "CONTAINS");
            }

            // Adım 3: İlk kelime eşleşmesi
            var firstWord = normalizedFormax.Split(' ')[0];
            if (firstWord.Length >= 4)
            {
                foreach (var af in afTeams)
                {
                    if (Normalize(af.TeamName).StartsWith(firstWord))
                        return new BestMatch(af.TeamId, af.TeamName, "PREFIX");
                }
            }

            return null;
        }

        // Türkçe → ASCII normalizasyon
        internal static string Normalize(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";
            return input
                .ToLowerInvariant()
                .Replace('ç', 'c')
                .Replace('ş', 's')
                .Replace('ğ', 'g')
                .Replace('ı', 'i')
                .Replace('ö', 'o')
                .Replace('ü', 'u')
                .Replace('â', 'a')
                .Replace('î', 'i')
                .Replace('û', 'u')
                .Trim();
        }

        private sealed record AfTeam(int TeamId, string TeamName, string Country);
        private sealed record BestMatch(int TeamId, string TeamName, string Method);

        private sealed class MappingResult
        {
            public int FormaxTeamId { get; set; }
            public string FormaxName { get; set; } = "";
            public int? ApiFootballTeamId { get; set; }
            public string? ApiFootballName { get; set; }
            public string MatchMethod { get; set; } = "";
        }
    }
}
