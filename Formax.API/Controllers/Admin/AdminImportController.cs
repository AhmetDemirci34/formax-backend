using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Formax.Infrastructure.Data;
using Formax.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Formax.API.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/import")]
    public class AdminImportController : ControllerBase
    {
        private readonly FormaxDbContext _db;

        public AdminImportController(FormaxDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// CSV ile bitmiş maç sonucu import eder.
        /// Multipart form-data: file
        /// Beklenen header:
        /// MatchDate,HomeTeam,AwayTeam,HomeScore,AwayScore
        /// </summary>
        [HttpPost("matches/csv")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(50_000_000)]
        public async Task<IActionResult> ImportMatchesCsv(IFormFile file, [FromQuery] bool dryRun = false)
        {
            if (file == null || file.Length == 0)
                return BadRequest("file boş.");

            var rows = new List<Row>();

            using (var stream = file.OpenReadStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
            {
                var header = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(header))
                    return BadRequest("CSV header yok.");

                var headerCols = SplitCsvLine(header).Select(x => x.Trim()).ToArray();
                var idx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < headerCols.Length; i++)
                    idx[headerCols[i]] = i;

                string[] required = { "MatchDate", "HomeTeam", "AwayTeam", "HomeScore", "AwayScore" };
                foreach (var r in required)
                    if (!idx.ContainsKey(r)) return BadRequest($"Eksik kolon: {r}");

                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var cols = SplitCsvLine(line);
                    if (cols.Length < headerCols.Length) continue;

                    rows.Add(new Row
                    {
                        MatchDateRaw = cols[idx["MatchDate"]]?.Trim(),
                        HomeTeam = cols[idx["HomeTeam"]]?.Trim(),
                        AwayTeam = cols[idx["AwayTeam"]]?.Trim(),
                        HomeScoreRaw = cols[idx["HomeScore"]]?.Trim(),
                        AwayScoreRaw = cols[idx["AwayScore"]]?.Trim()
                    });
                }
            }

            if (rows.Count == 0)
                return BadRequest("CSV satırı yok.");

            var existingTeams = await _db.Teams.AsNoTracking().ToListAsync();
            var teamByName = existingTeams
                .GroupBy(t => (t.Name ?? "").Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            int createdTeams = 0;
            int createdMatches = 0;
            int skipped = 0;
            var errors = new List<string>();

            var recentMatches = await _db.Matches.AsNoTracking()
                .OrderByDescending(m => m.MatchDate)
                .Take(5000)
                .ToListAsync();

            foreach (var r in rows)
            {
                if (!TryParseDateUtc(r.MatchDateRaw, out var matchUtc))
                {
                    errors.Add($"Tarih parse edilemedi: '{r.MatchDateRaw}'");
                    continue;
                }

                if (!int.TryParse(r.HomeScoreRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hs) ||
                    !int.TryParse(r.AwayScoreRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var aS))
                {
                    errors.Add($"Skor parse edilemedi: '{r.HomeScoreRaw}'-'{r.AwayScoreRaw}'");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(r.HomeTeam) || string.IsNullOrWhiteSpace(r.AwayTeam))
                {
                    errors.Add("HomeTeam/AwayTeam boş.");
                    continue;
                }

                var home = await GetOrCreateTeam(teamByName, r.HomeTeam!, dryRun);
                if (home.Created) createdTeams++;

                var away = await GetOrCreateTeam(teamByName, r.AwayTeam!, dryRun);
                if (away.Created) createdTeams++;

                var dup = recentMatches.Any(m =>
                    m.MatchDate == matchUtc &&
                    m.HomeTeamId == home.Team.Id &&
                    m.AwayTeamId == away.Team.Id &&
                    m.HomeScore == hs &&
                    m.AwayScore == aS);

                if (dup)
                {
                    skipped++;
                    continue;
                }

                if (!dryRun)
                {
                    _db.Matches.Add(new Match
                    {
                        HomeTeamId = home.Team.Id,
                        AwayTeamId = away.Team.Id,
                        MatchDate = matchUtc,
                        HomeScore = hs,
                        AwayScore = aS,
                        MatchMinute = null,
                        Status = "Finished",
                        CreatedAt = DateTime.UtcNow
                    });
                }

                createdMatches++;
            }

            if (!dryRun)
                await _db.SaveChangesAsync();

            return Ok(new
            {
                dryRun,
                inputRows = rows.Count,
                createdTeams,
                createdMatches,
                skipped,
                errorCount = errors.Count,
                errors = errors.Take(50).ToList(),
                note = "Bu import sadece bitmiş maç sonuç datasıdır. Bahis/tahmin değildir."
            });
        }

        private async Task<(Team Team, bool Created)> GetOrCreateTeam(
            Dictionary<string, Team> teamByName,
            string name,
            bool dryRun)
        {
            var key = name.Trim();

            if (teamByName.TryGetValue(key, out var existing))
                return (existing, false);

            var team = new Team
            {
                Name = key,
                LeagueRank = 0,
                IsStableTeam = false,
                AvgGoalsFor = 0,
                AvgGoalsAgainst = 0
            };

            if (!dryRun)
            {
                _db.Teams.Add(team);
                await _db.SaveChangesAsync();
            }
            else
            {
                team.Id = -Math.Abs(key.GetHashCode());
            }

            teamByName[key] = team;
            return (team, true);
        }

        private static bool TryParseDateUtc(string? raw, out DateTime utc)
        {
            utc = default;
            if (string.IsNullOrWhiteSpace(raw)) return false;

            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt))
            {
                utc = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                return true;
            }

            if (DateTime.TryParseExact(raw,
                    new[] { "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm" },
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out dt))
            {
                utc = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                return true;
            }

            return false;
        }

        private static string[] SplitCsvLine(string line)
        {
            var result = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                var c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                        continue;
                    }
                    inQuotes = !inQuotes;
                    continue;
                }

                if (c == ',' && !inQuotes)
                {
                    result.Add(sb.ToString());
                    sb.Clear();
                    continue;
                }

                sb.Append(c);
            }

            result.Add(sb.ToString());
            return result.ToArray();
        }

        private sealed class Row
        {
            public string? MatchDateRaw { get; set; }
            public string? HomeTeam { get; set; }
            public string? AwayTeam { get; set; }
            public string? HomeScoreRaw { get; set; }
            public string? AwayScoreRaw { get; set; }
        }
    }
}