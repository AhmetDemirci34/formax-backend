using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.OfficialSources;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Infrastructure.BackgroundJobs;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.OfficialSources
{
    public sealed record OfficialPostMatchMatchOutcome(int MatchId, string SourceKey, string Events, string Statistics);

    public sealed record OfficialPostMatchCycleResult(
        int Candidates, int EventRowsWritten, int StatisticsRowsWritten, IReadOnlyList<OfficialPostMatchMatchOutcome> Matches);

    /// <summary>
    /// BİTMİŞ MAÇ OLAY + İSTATİSTİK — RESMÎ KAYNAKTAN (API-Football fixtures/events ve
    /// fixtures/statistics ÇAĞRILMAZ).
    ///
    /// Aday: resmî kaynaktan sonucu yazılmış, son 96 saatte bitmiş, resmî bağlantısı olan maçlar.
    /// Olay ve istatistik ayrı yazılır; kaynak bir alanı vermiyorsa NULL kalır (sıfır uydurulmaz);
    /// kaynak istatistik yayımlamıyorsa (Serie A) satır YAZILMAZ. Aynı olay iki kez yazılmaz
    /// (maç + resmî olay kimliği), aynı taraf istatistiği iki kez yazılmaz (maç + taraf).
    /// Okuma yolu (maç özeti) bu servisi ÇAĞIRMAZ; yalnız DB'yi okur.
    /// </summary>
    public sealed class OfficialPostMatchDataService
    {
        private readonly FormaxDbContext _db;
        private readonly IReadOnlyList<IOfficialCompetitionSource> _sources;
        private readonly IConfiguration _config;
        private readonly ILogger<OfficialPostMatchDataService> _log;

        public OfficialPostMatchDataService(
            FormaxDbContext db, IEnumerable<IOfficialCompetitionSource> sources, IConfiguration config,
            ILogger<OfficialPostMatchDataService> log)
        {
            _db = db;
            _sources = sources.ToList();
            _config = config;
            _log = log;
        }

        public async Task<OfficialPostMatchCycleResult> RunCycleAsync(DateTime utcNow, CancellationToken ct = default)
        {
            var since = utcNow.AddHours(-Math.Max(24, _config.GetValue("OfficialSources:PostMatch:LookbackHours", 96)));
            var max = Math.Max(0, _config.GetValue("OfficialSources:PostMatch:MaxMatchesPerCycle", 12));
            var allow = CoveragePolicy.LeagueAllowList(_config);

            var finished = (await _db.Matches.AsNoTracking()
                    .Include(m => m.HomeTeam).Include(m => m.AwayTeam)
                    .Where(m => m.Status == MatchStatuses.Finished && m.MatchDate >= since && m.MatchDate <= utcNow
                                && m.ResultSource != null && m.ResultSource.StartsWith("official:"))
                    .OrderByDescending(m => m.MatchDate)
                    .ToListAsync(ct))
                .Where(m => CoveragePolicy.Allows(allow, m.LeagueId))
                .ToList();

            var outcomes = new List<OfficialPostMatchMatchOutcome>();
            int eventRows = 0, statRows = 0, processed = 0;
            var roundKey = $"postmatch:{utcNow:yyyyMMddHHmm}";

            foreach (var match in finished)
            {
                if (processed >= max) break;
                var sourceKey = match.ResultSource!.Substring(OfficialLineupCollector.ProviderPrefix.Length);
                var source = _sources.FirstOrDefault(s => s.SourceKey == sourceKey) as IOfficialPostMatchSource;
                var descriptor = OfficialSourceRegistry.ByKey(sourceKey);
                if (source == null || descriptor?.Status != OfficialSourceStatuses.Verified) continue;

                var wantsEvents = descriptor.Capabilities.Contains(OfficialPurposes.Events)
                                  && !await _db.MatchEventRecords.AnyAsync(e => e.MatchId == match.Id, ct);
                var wantsStats = descriptor.Capabilities.Contains(OfficialPurposes.Statistics)
                                 && !await _db.MatchTeamStatistics.AnyAsync(s => s.MatchId == match.Id, ct);
                if (!wantsEvents && !wantsStats) continue;

                var link = await _db.OfficialMatchLinks.AsNoTracking()
                    .FirstOrDefaultAsync(l => l.MatchId == match.Id && l.SourceKey == sourceKey, ct);
                if (link == null) continue;

                processed++;
                var record = new OfficialMatchRecord(sourceKey, link.OfficialMatchId, link.OfficialUrl,
                    link.OfficialHomeName, link.OfficialAwayName, link.OfficialKickoffUtc, OfficialMatchStatuses.Finished,
                    match.HomeScore, match.AwayScore, null);
                var round = new OfficialRoundContext(roundKey, utcNow, OfficialPurposes.Events, match.Id);

                string eventsOutcome = "Skipped", statsOutcome = "Skipped";
                if (wantsEvents)
                {
                    var read = await source.ReadEventsAsync(record, round, ct);
                    if (!read.Ok) eventsOutcome = read.Outcome + ":" + read.Detail;
                    else
                    {
                        var written = await WriteEventsAsync(match, sourceKey, read.Value ?? Array.Empty<OfficialMatchEvent>(), utcNow, ct);
                        eventRows += written;
                        eventsOutcome = $"Written:{written}";
                    }
                }
                if (wantsStats)
                {
                    var read = await source.ReadStatisticsAsync(record, round with { Purpose = OfficialPurposes.Statistics }, ct);
                    if (read.Outcome == OfficialReadOutcomes.NotSupported) statsOutcome = "NotPublishedBySource";
                    else if (!read.Ok) statsOutcome = read.Outcome + ":" + read.Detail;
                    else if (read.Value == null) statsOutcome = "EmptyFromSource";
                    else
                    {
                        var written = await WriteStatisticsAsync(match, sourceKey, read.Value, utcNow, ct);
                        statRows += written;
                        statsOutcome = $"Written:{written}";
                    }
                }
                outcomes.Add(new OfficialPostMatchMatchOutcome(match.Id, sourceKey, eventsOutcome, statsOutcome));
            }

            if (outcomes.Count > 0)
                _log.LogInformation("[OFFICIAL POST-MATCH] {Count} maç: {Summary}", outcomes.Count,
                    string.Join(", ", outcomes.Select(o => $"{o.MatchId} olay={o.Events} ist={o.Statistics}")));
            return new OfficialPostMatchCycleResult(finished.Count, eventRows, statRows, outcomes);
        }

        private async Task<int> WriteEventsAsync(Match match, string sourceKey, IReadOnlyList<OfficialMatchEvent> events,
            DateTime utcNow, CancellationToken ct)
        {
            var existing = new HashSet<string>(await _db.MatchEventRecords.Where(e => e.MatchId == match.Id)
                .Select(e => e.ProviderEventId).ToListAsync(ct), StringComparer.Ordinal);
            var written = 0;
            foreach (var e in events)
            {
                var key = e.OfficialEventId.Length > 120 ? e.OfficialEventId[..120] : e.OfficialEventId;
                if (!existing.Add(key)) continue;
                _db.MatchEventRecords.Add(new MatchEventRecord
                {
                    MatchId = match.Id,
                    ExternalFixtureId = match.ExternalMatchId ?? string.Empty,
                    ProviderEventId = key,
                    Minute = e.Minute,
                    ExtraMinute = e.ExtraMinute,
                    TeamName = e.Side == "home" ? match.HomeTeam?.Name : match.AwayTeam?.Name,
                    PlayerName = e.PlayerName,
                    AssistName = e.AssistName,
                    EventType = e.EventType,
                    Detail = e.Detail,
                    Source = OfficialLineupCollector.ProviderPrefix + sourceKey,
                    FetchedAtUtc = utcNow
                });
                written++;
            }
            await _db.SaveChangesAsync(ct);
            return written;
        }

        private async Task<int> WriteStatisticsAsync(Match match, string sourceKey, OfficialMatchStatistics stats,
            DateTime utcNow, CancellationToken ct)
        {
            var written = 0;
            foreach (var (side, s, name) in new[] { ("Home", stats.Home, match.HomeTeam?.Name), ("Away", stats.Away, match.AwayTeam?.Name) })
            {
                if (!s.HasAnyMeasurement) continue;
                if (await _db.MatchTeamStatistics.AnyAsync(x => x.MatchId == match.Id && x.Side == side, ct)) continue;
                _db.MatchTeamStatistics.Add(new MatchTeamStatistic
                {
                    MatchId = match.Id,
                    ExternalFixtureId = match.ExternalMatchId ?? string.Empty,
                    Side = side,
                    TeamName = name,
                    BallPossession = s.BallPossession,
                    TotalShots = s.TotalShots,
                    ShotsOnTarget = s.ShotsOnTarget,
                    ShotsOffTarget = s.ShotsOffTarget,
                    BlockedShots = s.BlockedShots,
                    Corners = s.Corners,
                    Offsides = s.Offsides,
                    Fouls = s.Fouls,
                    YellowCards = s.YellowCards,
                    RedCards = s.RedCards,
                    GoalkeeperSaves = s.GoalkeeperSaves,
                    TotalPasses = s.TotalPasses,
                    AccuratePasses = s.AccuratePasses,
                    PassAccuracy = s.PassAccuracy,
                    Source = OfficialLineupCollector.ProviderPrefix + sourceKey,
                    FetchedAtUtc = utcNow
                });
                written++;
            }
            await _db.SaveChangesAsync(ct);
            return written;
        }
    }
}
