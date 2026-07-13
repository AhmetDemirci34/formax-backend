using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.Historical;

/// <summary>
/// Tarihsel CSV import motoru (Matches.csv + EloRatings.csv → kanonik Historical domain).
/// Mevcut GDP resolver/persister pattern'ine sadık: ada göre çöz-veya-oluştur, SourceKey ile idempotent.
/// Performans: in-memory dict cache (tekrar DB sorgusu yok), tek transaction, batch SaveChanges +
/// ChangeTracker.Clear (bellek sınırlı), AutoDetectChanges kapalı. İkinci çalıştırmada 0 yeni kayıt.
/// </summary>
public sealed class HistoricalImportService : IHistoricalImportService
{
    private const int BatchSize = 2000;
    private const int ProgressEvery = 20000;

    private readonly FormaxDbContext _db;
    private readonly ILogger<HistoricalImportService> _logger;

    public HistoricalImportService(FormaxDbContext db, ILogger<HistoricalImportService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<HistoricalImportSummary> ImportAsync(string matchesCsvPath, string eloCsvPath, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        _db.ChangeTracker.AutoDetectChangesEnabled = false;

        // ── Mevcut durumu belleğe al (idempotency + tekrar sorgu yok) ──
        var compByDivision = await _db.HistoricalCompetitions
            .AsNoTracking().ToDictionaryAsync(c => c.Division, c => c.Id, cancellationToken).ConfigureAwait(false);
        var teamByKey = await _db.HistoricalTeams
            .AsNoTracking().ToDictionaryAsync(t => t.NormalizedKey, t => t.Id, cancellationToken).ConfigureAwait(false);
        var matchKeys = new HashSet<string>(
            await _db.HistoricalMatches.AsNoTracking().Select(m => m.SourceKey).ToListAsync(cancellationToken).ConfigureAwait(false));
        var eloKeys = new HashSet<string>(
            await _db.HistoricalEloRatings.AsNoTracking().Select(e => e.SourceKey).ToListAsync(cancellationToken).ConfigureAwait(false));

        var compCreated = 0;
        var teamCreated = 0;

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var matchStats = await ImportMatchesAsync(matchesCsvPath, compByDivision, teamByKey, matchKeys,
                () => compCreated++, () => teamCreated++, cancellationToken).ConfigureAwait(false);

            var eloStats = await ImportEloAsync(eloCsvPath, teamByKey, eloKeys, cancellationToken).ConfigureAwait(false);

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            sw.Stop();

            var peakMb = Process.GetCurrentProcess().PeakWorkingSet64 / (1024 * 1024);
            var summary = new HistoricalImportSummary
            {
                MatchesRead = matchStats.read,
                MatchesImported = matchStats.imported,
                MatchesDuplicate = matchStats.duplicate,
                MatchesSkipped = matchStats.skipped,
                EloRead = eloStats.read,
                EloImported = eloStats.imported,
                EloDuplicate = eloStats.duplicate,
                EloMatchedToTeam = eloStats.matched,
                CompetitionsCreated = compCreated,
                TeamsCreated = teamCreated,
                ElapsedMs = sw.ElapsedMilliseconds,
                PeakMemoryMB = peakMb
            };

            _logger.LogInformation(
                "Historical import BİTTİ: maç {Imp}/{Read} (dup {Dup}, skip {Skip}), elo {EImp}/{ERead} (dup {EDup}, matched {EMatch}), comp {C}, team {T}, {Ms} ms, {Mem} MB",
                summary.MatchesImported, summary.MatchesRead, summary.MatchesDuplicate, summary.MatchesSkipped,
                summary.EloImported, summary.EloRead, summary.EloDuplicate, summary.EloMatchedToTeam,
                summary.CompetitionsCreated, summary.TeamsCreated, summary.ElapsedMs, summary.PeakMemoryMB);

            return summary;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<(int read, int imported, int duplicate, int skipped)> ImportMatchesAsync(
        string path,
        Dictionary<string, int> compByDivision,
        Dictionary<string, int> teamByKey,
        HashSet<string> matchKeys,
        Action onCompCreated,
        Action onTeamCreated,
        CancellationToken ct)
    {
        int read = 0, imported = 0, duplicate = 0, skipped = 0;
        var buffer = new List<HistoricalMatch>(BatchSize);

        using var reader = new StreamReader(path, Encoding.UTF8);
        var header = await reader.ReadLineAsync().ConfigureAwait(false); // atla
        string? line;
        while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) is not null)
        {
            if (line.Length == 0) continue;
            read++;
            var f = SplitCsv(line);

            // Data cleaning: zorunlu alanlar (division/date/home/away) yoksa atla.
            var division = Get(f, 0);
            var home = Get(f, 3);
            var away = Get(f, 4);
            if (string.IsNullOrWhiteSpace(division) || string.IsNullOrWhiteSpace(home) || string.IsNullOrWhiteSpace(away)
                || !TryDate(Get(f, 1), out var date))
            {
                skipped++;
                continue;
            }

            var homeKey = HistoricalNameNormalizer.Normalize(home);
            var awayKey = HistoricalNameNormalizer.Normalize(away);
            if (homeKey.Length == 0 || awayKey.Length == 0) { skipped++; continue; }

            var sourceKey = $"{division}|{date:yyyy-MM-dd}|{homeKey}|{awayKey}";
            if (!matchKeys.Add(sourceKey)) { duplicate++; continue; }

            var compId = await ResolveCompetitionAsync(division, compByDivision, onCompCreated, ct).ConfigureAwait(false);
            var homeId = await ResolveTeamAsync(home, homeKey, teamByKey, onTeamCreated, ct).ConfigureAwait(false);
            var awayId = await ResolveTeamAsync(away, awayKey, teamByKey, onTeamCreated, ct).ConfigureAwait(false);

            buffer.Add(new HistoricalMatch
            {
                HistoricalCompetitionId = compId,
                HomeTeamId = homeId,
                AwayTeamId = awayId,
                MatchDate = date,
                MatchTime = NullIfEmpty(Get(f, 2)),
                HomeElo = Dbl(f, 5), AwayElo = Dbl(f, 6),
                Form3Home = Dbl(f, 7), Form5Home = Dbl(f, 8), Form3Away = Dbl(f, 9), Form5Away = Dbl(f, 10),
                FTHome = Int(f, 11), FTAway = Int(f, 12), FTResult = NullIfEmpty(Get(f, 13)),
                HTHome = Int(f, 14), HTAway = Int(f, 15), HTResult = NullIfEmpty(Get(f, 16)),
                HomeShots = Int(f, 17), AwayShots = Int(f, 18), HomeTarget = Int(f, 19), AwayTarget = Int(f, 20),
                HomeFouls = Int(f, 21), AwayFouls = Int(f, 22), HomeCorners = Int(f, 23), AwayCorners = Int(f, 24),
                HomeYellow = Int(f, 25), AwayYellow = Int(f, 26), HomeRed = Int(f, 27), AwayRed = Int(f, 28),
                OddHome = Dbl(f, 29), OddDraw = Dbl(f, 30), OddAway = Dbl(f, 31),
                MaxHome = Dbl(f, 32), MaxDraw = Dbl(f, 33), MaxAway = Dbl(f, 34),
                Over25 = Dbl(f, 35), Under25 = Dbl(f, 36), MaxOver25 = Dbl(f, 37), MaxUnder25 = Dbl(f, 38),
                HandiSize = Dbl(f, 39), HandiHome = Dbl(f, 40), HandiAway = Dbl(f, 41),
                SourceKey = sourceKey,
                LastUpdatedUtc = DateTime.UtcNow
            });
            imported++;

            if (buffer.Count >= BatchSize)
                await FlushMatchesAsync(buffer, ct).ConfigureAwait(false);

            if (read % ProgressEvery == 0)
                _logger.LogInformation("Historical maç ilerleme: {Read} okundu, {Imp} import, {Dup} dup, {Skip} skip", read, imported, duplicate, skipped);
        }

        if (buffer.Count > 0)
            await FlushMatchesAsync(buffer, ct).ConfigureAwait(false);

        return (read, imported, duplicate, skipped);
    }

    private async Task FlushMatchesAsync(List<HistoricalMatch> buffer, CancellationToken ct)
    {
        _db.HistoricalMatches.AddRange(buffer);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        _db.ChangeTracker.Clear();
        buffer.Clear();
    }

    private async Task<(int read, int imported, int duplicate, int matched)> ImportEloAsync(
        string path, Dictionary<string, int> teamByKey, HashSet<string> eloKeys, CancellationToken ct)
    {
        int read = 0, imported = 0, duplicate = 0, matched = 0;
        var buffer = new List<HistoricalEloRating>(BatchSize);

        using var reader = new StreamReader(path, Encoding.UTF8);
        await reader.ReadLineAsync().ConfigureAwait(false); // header
        string? line;
        while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) is not null)
        {
            if (line.Length == 0) continue;
            read++;
            var f = SplitCsv(line);

            var club = Get(f, 1);
            if (string.IsNullOrWhiteSpace(club) || !TryDate(Get(f, 0), out var date) || !TryDbl(Get(f, 3), out var elo))
                continue;

            var clubKey = HistoricalNameNormalizer.Normalize(club);
            if (clubKey.Length == 0) continue;

            var sourceKey = $"{clubKey}|{date:yyyy-MM-dd}";
            if (!eloKeys.Add(sourceKey)) { duplicate++; continue; }

            int? teamId = teamByKey.TryGetValue(clubKey, out var tid) ? tid : (int?)null;
            if (teamId is not null) matched++;

            buffer.Add(new HistoricalEloRating
            {
                HistoricalTeamId = teamId,
                Club = club,
                Country = NullIfEmpty(Get(f, 2)),
                Date = date,
                Elo = elo,
                SourceKey = sourceKey,
                LastUpdatedUtc = DateTime.UtcNow
            });
            imported++;

            if (buffer.Count >= BatchSize)
            {
                _db.HistoricalEloRatings.AddRange(buffer);
                await _db.SaveChangesAsync(ct).ConfigureAwait(false);
                _db.ChangeTracker.Clear();
                buffer.Clear();
            }

            if (read % ProgressEvery == 0)
                _logger.LogInformation("Historical elo ilerleme: {Read} okundu, {Imp} import, {Match} eşleşti", read, imported, matched);
        }

        if (buffer.Count > 0)
        {
            _db.HistoricalEloRatings.AddRange(buffer);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            _db.ChangeTracker.Clear();
        }

        return (read, imported, duplicate, matched);
    }

    private async Task<int> ResolveCompetitionAsync(string division, Dictionary<string, int> cache, Action onCreated, CancellationToken ct)
    {
        if (cache.TryGetValue(division, out var id)) return id;

        var entry = DivisionCatalog.Resolve(division);
        var comp = new HistoricalCompetition { Division = division, Name = entry.Name, Country = entry.Country, LastUpdatedUtc = DateTime.UtcNow };
        _db.HistoricalCompetitions.Add(comp);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        _db.Entry(comp).State = EntityState.Detached;
        cache[division] = comp.Id;
        onCreated();
        return comp.Id;
    }

    private async Task<int> ResolveTeamAsync(string rawName, string normKey, Dictionary<string, int> cache, Action onCreated, CancellationToken ct)
    {
        if (cache.TryGetValue(normKey, out var id)) return id;

        var team = new HistoricalTeam { Name = rawName.Trim(), NormalizedKey = normKey, LastUpdatedUtc = DateTime.UtcNow };
        _db.HistoricalTeams.Add(team);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        _db.Entry(team).State = EntityState.Detached;
        cache[normKey] = team.Id;
        onCreated();
        return team.Id;
    }

    // ── CSV + parse yardımcıları ──

    private static string[] SplitCsv(string line)
    {
        var result = new List<string>(48);
        var sb = new StringBuilder();
        var inQuotes = false;
        foreach (var ch in line)
        {
            if (ch == '"') { inQuotes = !inQuotes; continue; }
            if (ch == ',' && !inQuotes) { result.Add(sb.ToString()); sb.Clear(); continue; }
            sb.Append(ch);
        }
        result.Add(sb.ToString());
        return result.ToArray();
    }

    private static string Get(string[] f, int i) => i < f.Length ? f[i].Trim() : string.Empty;
    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s;

    private static int? Int(string[] f, int i)
        => int.TryParse(Get(f, i), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v
         : double.TryParse(Get(f, i), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? (int)d
         : (int?)null;

    private static double? Dbl(string[] f, int i)
        => double.TryParse(Get(f, i), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : (double?)null;

    private static bool TryDbl(string s, out double v)
        => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);

    private static bool TryDate(string s, out DateTime date)
        => DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out date);
}
