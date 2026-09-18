using Formax.Application.Interfaces;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Repositories;

/// <summary>
/// Batch-oriented repository for FixtureSyncJob.
/// Two queries per sync cycle regardless of fixture count.
/// </summary>
public class FixtureSyncRepository : IFixtureSyncRepository
{
    private readonly FormaxDbContext _context;

    public FixtureSyncRepository(FormaxDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public Dictionary<string, Team> GetTeamsByExternalIds(IEnumerable<string> externalIds)
    {
        var ids = externalIds.ToHashSet();
        if (ids.Count == 0) return new Dictionary<string, Team>();

        // DistinctBy guards against duplicate ExternalTeamId rows that could
        // exist in a dirty DB (race window before unique index was enforced).
        // AsEnumerable() materialises the WHERE result in memory first so that
        // DistinctBy runs client-side — safe because the set is bounded by ids.Count.
        return _context.Teams
            .Where(t => t.ExternalTeamId != null && ids.Contains(t.ExternalTeamId))
            .AsEnumerable()
            .DistinctBy(t => t.ExternalTeamId!)
            .ToDictionary(t => t.ExternalTeamId!, t => t);
    }

    /// <inheritdoc />
    public Dictionary<string, Match> GetMatchesByExternalIds(IEnumerable<string> externalIds)
    {
        var ids = externalIds.ToHashSet();
        if (ids.Count == 0) return new Dictionary<string, Match>();

        // Same duplicate-safe pattern as GetTeamsByExternalIds.
        return _context.Matches
            .Where(m => m.ExternalMatchId != null && ids.Contains(m.ExternalMatchId))
            .AsEnumerable()
            .DistinctBy(m => m.ExternalMatchId!)
            .ToDictionary(m => m.ExternalMatchId!, m => m);
    }

    /// <inheritdoc />
    public List<Match> GetStaleResultCandidates(
        DateTime nowUtc,
        int settleMarginMinutes,
        IReadOnlyCollection<int> leagueIds)
    {
        var cutoff = nowUtc.AddMinutes(-settleMarginMinutes);

        var q = _context.Matches.AsNoTracking()
            .Where(m => m.MatchDate <= cutoff
                     && (m.Status == "NotStarted" || m.Status == "Live"));

        if (leagueIds is { Count: > 0 })
        {
            var ids = leagueIds.ToList();
            q = q.Where(m => ids.Contains(m.LeagueId));
        }

        // En eski önce: en uzun süredir eksik kalan sonuç ilk sırada kapatılır.
        return q.OrderBy(m => m.MatchDate).ToList();
    }

    /// <inheritdoc />
    public HashSet<int> GetLeaguesWithVerifiedSeason()
        => _context.LeagueSeasons.AsNoTracking()
               .Select(s => s.LeagueId)
               .Distinct()
               .ToHashSet();

    /// <inheritdoc />
    public bool TryReserveFixtureAttempt(
        string externalMatchId, string purpose, TimeSpan cooldown, int maxPerUtcDay, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(externalMatchId) || maxPerUtcDay <= 0) return false;

        var day = nowUtc.Date;
        var cooldownCutoff = nowUtc - cooldown;

        // 1) SOĞUMA — bu fikstür için (gün fark etmeksizin) en son deneme.
        var lastAttempt = _context.FixtureRefreshAttempts.AsNoTracking()
            .Where(a => a.ExternalMatchId == externalMatchId && a.Purpose == purpose)
            .Select(a => (DateTime?)a.LastAttemptUtc)
            .Max();
        if (lastAttempt.HasValue && lastAttempt.Value > cooldownCutoff) return false;

        // 2) GÜNLÜK TAVAN — amaç bazında, UTC gün başına toplam deneme.
        var usedToday = _context.FixtureRefreshAttempts.AsNoTracking()
            .Where(a => a.Purpose == purpose && a.DayUtc == day)
            .Select(a => (int?)a.AttemptCount)
            .Sum() ?? 0;
        if (usedToday >= maxPerUtcDay) return false;

        // 3) ATOMİK REZERVASYON.
        //
        // İLİŞKİSEL SAĞLAYICI: koşullu tek yazım (UPDATE ... WHERE LastAttemptUtc <= cutoff).
        // İki süreç yarışırsa yalnız biri 1 satır etkiler; diğeri 0 alır ve istek YAPMAZ.
        // Satır yoksa INSERT edilir; kaybeden taraf PK ihlali alır ve false döner.
        // Kilit görevini veritabanı görür — süreç belleği bu garantiyi veremez.
        if (_context.Database.IsRelational())
        {
            var updated = _context.Database.ExecuteSqlInterpolated($@"
UPDATE [FixtureRefreshAttempts]
   SET [AttemptCount] = [AttemptCount] + 1,
       [LastAttemptUtc] = {nowUtc},
       [LastOutcome] = N'Reserved'
 WHERE [ExternalMatchId] = {externalMatchId}
   AND [Purpose] = {purpose}
   AND [DayUtc] = {day}
   AND [LastAttemptUtc] <= {cooldownCutoff}");
            if (updated == 1) return true;

            try
            {
                var inserted = _context.Database.ExecuteSqlInterpolated($@"
INSERT INTO [FixtureRefreshAttempts]
    ([ExternalMatchId],[Purpose],[DayUtc],[AttemptCount],[LastAttemptUtc],[LastOutcome])
SELECT {externalMatchId}, {purpose}, {day}, 1, {nowUtc}, N'Reserved'
 WHERE NOT EXISTS (SELECT 1 FROM [FixtureRefreshAttempts]
                    WHERE [ExternalMatchId] = {externalMatchId}
                      AND [Purpose] = {purpose}
                      AND [DayUtc] = {day})");
                return inserted == 1;
            }
            catch (DbUpdateException) { return false; }   // yarışı başka süreç kazandı
            catch (Microsoft.Data.SqlClient.SqlException) { return false; }
        }

        // İLİŞKİSEL OLMAYAN SAĞLAYICI (otomatik testler): aynı sözleşme, EF yazımıyla.
        // Eşzamanlılık garantisini bileşik birincil anahtar verir.
        var existing = _context.FixtureRefreshAttempts
            .FirstOrDefault(a => a.ExternalMatchId == externalMatchId && a.Purpose == purpose && a.DayUtc == day);
        try
        {
            if (existing != null)
            {
                if (existing.LastAttemptUtc > cooldownCutoff) return false;
                existing.AttemptCount += 1;
                existing.LastAttemptUtc = nowUtc;
                existing.LastOutcome = "Reserved";
            }
            else
            {
                _context.FixtureRefreshAttempts.Add(new FixtureRefreshAttempt
                {
                    ExternalMatchId = externalMatchId,
                    Purpose = purpose,
                    DayUtc = day,
                    AttemptCount = 1,
                    LastAttemptUtc = nowUtc,
                    LastOutcome = "Reserved"
                });
            }
            _context.SaveChanges();
            return true;
        }
        catch (DbUpdateException) { return false; }
    }

    /// <inheritdoc />
    public void RecordFixtureAttemptOutcome(string externalMatchId, string purpose, DateTime nowUtc, string outcome)
    {
        var day = nowUtc.Date;
        var row = _context.FixtureRefreshAttempts
            .FirstOrDefault(a => a.ExternalMatchId == externalMatchId && a.Purpose == purpose && a.DayUtc == day);
        if (row == null) return;
        row.LastOutcome = outcome;
    }

    /// <inheritdoc />
    public async Task<int> GetFixtureAttemptCountAsync(
        string externalMatchId, string purpose, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(externalMatchId)) return 0;

        // GÜN AYRIMI YOK: slot takvimi "bu maç için toplam kaç kez denendi?" diye sorar.
        // Gün başına toplama, dün harcanmış bir slotu bugün geri açardı.
        return await _context.FixtureRefreshAttempts.AsNoTracking()
            .Where(a => a.ExternalMatchId == externalMatchId && a.Purpose == purpose)
            .SumAsync(a => (int?)a.AttemptCount, ct)
            .ConfigureAwait(false) ?? 0;
    }

    /// <inheritdoc />
    public Formax.Application.Services.PostMatch.FixtureAttemptSummary GetFixtureAttemptSummary(
        string externalMatchId, string purpose)
    {
        if (string.IsNullOrWhiteSpace(externalMatchId))
            return Formax.Application.Services.PostMatch.FixtureAttemptSummary.None;

        var rows = _context.FixtureRefreshAttempts.AsNoTracking()
            .Where(a => a.ExternalMatchId == externalMatchId && a.Purpose == purpose)
            .Select(a => new { a.AttemptCount, a.LastAttemptUtc, a.LastOutcome, a.DayUtc })
            .ToList();
        if (rows.Count == 0) return Formax.Application.Services.PostMatch.FixtureAttemptSummary.None;

        var latest = rows.OrderByDescending(r => r.LastAttemptUtc).First();
        return new Formax.Application.Services.PostMatch.FixtureAttemptSummary(
            Attempts: rows.Sum(r => r.AttemptCount),
            LastAttemptUtc: latest.LastAttemptUtc,
            LastOutcome: latest.LastOutcome,
            ExhaustedRecorded: rows.Any(r => r.LastOutcome == Formax.Domain.Constants.MatchVideoVerificationStatuses.Unavailable));
    }

    /// <inheritdoc />
    public void RecordFixtureAttemptBlocked(string externalMatchId, string purpose, DateTime nowUtc, string outcome)
    {
        var day = nowUtc.Date;
        var row = _context.FixtureRefreshAttempts
            .FirstOrDefault(a => a.ExternalMatchId == externalMatchId && a.Purpose == purpose && a.DayUtc == day);
        if (row == null) return;
        // Rezervasyonun saydığı deneme geri alınır; sıfırın altına inilmez.
        if (row.AttemptCount > 0) row.AttemptCount--;
        row.LastOutcome = outcome;
    }

    /// <inheritdoc />
    public DateTime? GetLastFixtureAttemptUtc(string externalMatchId, string purpose)
    {
        if (string.IsNullOrWhiteSpace(externalMatchId)) return null;

        // Gün başına ayrı satır tutulur; en yeni deneme anı hepsinin maksimumudur.
        return _context.FixtureRefreshAttempts.AsNoTracking()
            .Where(a => a.ExternalMatchId == externalMatchId && a.Purpose == purpose)
            .Max(a => (DateTime?)a.LastAttemptUtc);
    }

    /// <inheritdoc />
    public List<Match> GetFutureScheduleRefreshCandidates(
        DateTime nowUtc, DateTime horizonUtc, IReadOnlyCollection<int> leagueIds)
    {
        var q = _context.Matches.AsNoTracking()
            .Where(m => m.ExternalMatchId != null && m.ExternalMatchId != ""
                     && m.Status == MatchStatuses.NotStarted
                     && m.MatchDate > nowUtc
                     && m.MatchDate <= horizonUtc
                     // KESİNLEŞMİŞ kickoff yeniden sorulmaz — bütçe geçici olanlara gider.
                     && m.KickoffPrecision == KickoffPrecisions.Provisional);

        if (leagueIds is { Count: > 0 })
        {
            var ids = leagueIds.ToList();
            q = q.Where(m => ids.Contains(m.LeagueId));
        }

        // SIRALAMA: en yakın nominal tarih önce (UI'ın gördüğü güne en yakın olan
        // önceliklidir). İkincil anahtar ZORUNLUDUR: yer-tutucu tarihler bir TURUN
        // TAMAMINA aynı değeri verir — ölçüldü 01.09.2026, 9 Süper Lig adayının
        // hepsi "2026-09-06 12:00:00". Yalnız MatchDate'e göre sıralamak, hangi 5
        // maçın bugünkü bütçeyi alacağını veritabanının keyfine bırakırdı; iki tur
        // arasında sıra değişir ve bazı maçlar sürekli ertelenebilirdi.
        return q.OrderBy(m => m.MatchDate).ThenBy(m => m.Id).ToList();
    }

    /// <inheritdoc />
    public List<Formax.Application.Services.Fixtures.CanonicalMatchCandidate> GetCanonicalCandidates(
        IReadOnlyCollection<int> leagueIds, DateTime fromUtc, DateTime toUtc)
    {
        if (leagueIds == null || leagueIds.Count == 0)
            return new List<Formax.Application.Services.Fixtures.CanonicalMatchCandidate>();
        var ids = leagueIds.ToHashSet();
        return _context.Matches.AsNoTracking()
            .Where(m => ids.Contains(m.LeagueId) && m.MatchDate >= fromUtc && m.MatchDate <= toUtc)
            .Select(m => new Formax.Application.Services.Fixtures.CanonicalMatchCandidate(
                m.Id, m.LeagueId, m.HomeTeamId, m.AwayTeamId, m.MatchDate, m.ExternalMatchId))
            .ToList();
    }

    /// <inheritdoc />
    public Match? GetTrackedMatch(int matchId)
        => _context.Matches.FirstOrDefault(m => m.Id == matchId);

    /// <inheritdoc />
    public void AddTeam(Team team)
        => _context.Teams.Add(team);

    /// <inheritdoc />
    public void AddMatch(Match match)
        => _context.Matches.Add(match);

    /// <inheritdoc />
    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
