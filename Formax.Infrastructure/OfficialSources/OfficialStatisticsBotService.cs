using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.OfficialSources;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.OfficialSources
{
    public sealed record StatisticsBotMatchOutcome(int MatchId, string Outcome, string? SourceKey, string Completeness);

    public sealed record StatisticsBotCycleReport(int Enqueued, int Claimed, int Written, IReadOnlyList<StatisticsBotMatchOutcome> Matches);

    /// <summary>
    /// API'SİZ RESMÎ İSTATİSTİK BOTU — kesin sonuç doğrulandıktan sonra (+10 dk … +24 sa, sonra günlük) maçın resmî istatistik
    /// kaynaklarını öncelik sırasıyla (lig/federasyon maç merkezi → turnuva → kulüp → yayıncı) okur.
    ///
    /// KESİN VERİ KURALI: kaynak alanı yayımlamadıysa null; açıkça 0 verdiyse 0. Eksik alan başka istatistikten tahmin edilmez.
    /// Aynı alan iki resmî kaynakta farklıysa yüksek öncelikli kaynak kanonik kalır, fark çelişki olarak deftere yazılır.
    /// Ev/deplasman yönü kaynağın maç kaydıyla sınanır (ters yön okunmaz). Aynı gözlem ikinci kez yazılmaz.
    /// </summary>
    public sealed class OfficialStatisticsBotService
    {
        public static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(5);

        private readonly FormaxDbContext _db;
        private readonly IReadOnlyList<IOfficialCompetitionSource> _sources;
        private readonly OfficialDataSourceCatalog _catalog;
        private readonly IConfiguration _config;
        private readonly ILogger<OfficialStatisticsBotService> _log;
        private readonly string _owner = Guid.NewGuid().ToString("N");

        public OfficialStatisticsBotService(FormaxDbContext db, IEnumerable<IOfficialCompetitionSource> sources,
            OfficialDataSourceCatalog catalog, IConfiguration config, ILogger<OfficialStatisticsBotService> log)
        {
            _db = db; _sources = sources.ToList(); _catalog = catalog; _config = config; _log = log;
        }

        private int MaxPerCycle => Math.Clamp(_config.GetValue("OfficialSources:StatisticsBot:MaxMatchesPerCycle", 20), 1, 200);

        public async Task<StatisticsBotCycleReport> RunCycleAsync(DateTime nowUtc, CancellationToken ct = default)
        {
            var enqueued = await EnqueueMissingAsync(nowUtc, ct).ConfigureAwait(false);
            var claimed = await ClaimDueAsync(nowUtc, ct).ConfigureAwait(false);
            if (claimed.Count == 0) return new(enqueued, 0, 0, Array.Empty<StatisticsBotMatchOutcome>());

            var roundKey = $"statsbot:{nowUtc:yyyyMMddHHmm}";
            var ids = claimed.Select(c => c.MatchId).ToList();
            var matches = await _db.Matches.AsNoTracking().Include(m => m.HomeTeam).Include(m => m.AwayTeam)
                .Where(m => ids.Contains(m.Id)).ToDictionaryAsync(m => m.Id, ct).ConfigureAwait(false);
            var outcomes = new List<StatisticsBotMatchOutcome>();
            var written = 0;

            foreach (var check in claimed)
            {
                ct.ThrowIfCancellationRequested();
                if (!matches.TryGetValue(check.MatchId, out var match) || match.Status != MatchStatuses.Finished)
                {
                    check.State = "Exhausted"; check.LastOutcome = "MatchNotFinal"; Release(check, nowUtc);
                    continue;
                }

                var descriptors = OfficialSourceRegistry.VerifiedFor(match.LeagueId, OfficialPurposes.Statistics);
                string outcome = "NoStatisticsSource";
                string? usedSource = null;
                var bestCompleteness = check.Completeness;

                foreach (var d in descriptors)
                {
                    if (_sources.FirstOrDefault(s => s.SourceKey == d.Key) is not IOfficialPostMatchSource src) continue;
                    var health = await _catalog.GetAsync(d.Key, ct).ConfigureAwait(false);
                    if (OfficialDataSourceCatalog.IsCircuitOpen(health, nowUtc)) { outcome = d.Key + ":CircuitOpen"; continue; }

                    var link = await _db.OfficialMatchLinks.AsNoTracking()
                        .FirstOrDefaultAsync(l => l.MatchId == match.Id && l.SourceKey == d.Key, ct).ConfigureAwait(false);
                    if (link == null) { outcome = d.Key + ":NoOfficialLink"; continue; }

                    var record = new OfficialMatchRecord(d.Key, link.OfficialMatchId, link.OfficialUrl, link.OfficialHomeName, link.OfficialAwayName,
                        link.OfficialKickoffUtc, OfficialMatchStatuses.Finished, match.HomeScore, match.AwayScore, null);
                    // Yön: kaynağın bağlantıdaki takımları FORMAX ev/deplasmanıyla aynı yönde olmalı.
                    if (!(OfficialMatchIdentityResolver.HomeMatches(record, match.HomeTeam?.Name ?? "")
                          && OfficialMatchIdentityResolver.AwayMatches(record, match.AwayTeam?.Name ?? "")))
                    {
                        outcome = d.Key + ":OrientationMismatch";
                        continue;
                    }

                    OfficialRead<OfficialMatchStatistics> read;
                    try
                    {
                        read = await src.ReadStatisticsAsync(record, new OfficialRoundContext(roundKey, nowUtc, OfficialPurposes.Statistics, match.Id), ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                    catch (Exception ex)
                    {
                        read = new OfficialRead<OfficialMatchStatistics>(null, OfficialReadOutcomes.ParseFailed, ex.GetType().Name + ": " + ex.Message, null);
                    }
                    await _catalog.RecordReadAsync(d.Key, read, nowUtc, ct).ConfigureAwait(false);
                    usedSource = d.Key;
                    if (read.Outcome == OfficialReadOutcomes.NotSupported) { outcome = d.Key + ":NotPublishedBySource"; continue; }
                    if (!read.Ok) { outcome = d.Key + ":" + read.Outcome + ":" + read.Detail; continue; }
                    if (read.Value == null) { outcome = d.Key + ":NotYetPublished" + (read.Detail == null ? "" : ":" + read.Detail); continue; }

                    var result = await WriteAsync(match, d.Key, (int)d.Tier, link.OfficialUrl, read.Value, nowUtc, ct).ConfigureAwait(false);
                    written += result.Written;
                    var completeness = StatisticsCompleteness.Of(read.Value);
                    if (Rank(completeness) > Rank(bestCompleteness)) bestCompleteness = completeness;
                    outcome = d.Key + ":" + result.Decision;
                    if (completeness == StatisticsCompleteness.Full) break;
                }

                check.AttemptCount++;
                check.Completeness = bestCompleteness;
                check.LastOutcome = outcome.Length > 160 ? outcome[..160] : outcome;
                check.LastSourceKey = usedSource;
                if (bestCompleteness == StatisticsCompleteness.Full)
                    check.State = "Complete";
                else
                {
                    var next = OfficialStatisticsSchedule.NextCheck(check.FinalResultAtUtc, check.AttemptCount, nowUtc);
                    if (next == null) check.State = "Exhausted";
                    else check.NextCheckUtc = next.Value;
                }
                Release(check, nowUtc);
                outcomes.Add(new(match.Id, outcome, usedSource, bestCompleteness));
            }

            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            _log.LogInformation("[STATS BOT] tur {Round}: plan+{Enq} işlenen={Claimed} yazılan={Written} {Outcomes}",
                roundKey, enqueued, claimed.Count, written, string.Join(", ", outcomes.Select(o => $"{o.MatchId}:{o.Outcome}:{o.Completeness}")));
            return new(enqueued, claimed.Count, written, outcomes);
        }

        private static int Rank(string c) => c switch { StatisticsCompleteness.Full => 2, StatisticsCompleteness.Partial => 1, _ => 0 };

        private static void Release(MatchStatisticsCheck c, DateTime nowUtc)
        {
            c.LastCheckUtc = nowUtc; c.UpdatedAtUtc = nowUtc; c.LockOwner = null; c.LockedUntilUtc = null;
        }

        /// <summary>
        /// Resmî kaynaktan sonucu yazılmış, son 72 saatte bitmiş ama istatistik planı olmayan maçlara plan açılır (ör. bot
        /// devreye girmeden önce yazılmış sonuçlar ya da restart sırasında kaçan adım).
        /// </summary>
        public async Task<int> EnqueueMissingAsync(DateTime nowUtc, CancellationToken ct = default)
        {
            var since = nowUtc.AddHours(-72);
            var rows = await _db.Matches.AsNoTracking()
                .Where(m => m.Status == MatchStatuses.Finished && m.MatchDate >= since && m.MatchDate <= nowUtc
                            && m.ResultSource != null && m.ResultSource.StartsWith("official:")
                            && LockedCompetitions.All.Contains(m.LeagueId)
                            && !_db.MatchStatisticsChecks.Any(s => s.MatchId == m.Id))
                .Select(m => new { m.Id, m.LeagueId, m.ResultUpdatedAtUtc, m.MatchDate }).Take(200).ToListAsync(ct).ConfigureAwait(false);
            foreach (var m in rows)
            {
                var final = m.ResultUpdatedAtUtc ?? m.MatchDate.AddMinutes(110);
                _db.MatchStatisticsChecks.Add(new MatchStatisticsCheck
                {
                    MatchId = m.Id, LeagueId = m.LeagueId, FinalResultAtUtc = final, State = "Pending",
                    NextCheckUtc = OfficialStatisticsSchedule.FirstCheck(final), CreatedAtUtc = nowUtc, UpdatedAtUtc = nowUtc
                });
            }
            if (rows.Count > 0) await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            return rows.Count;
        }

        public async Task<List<MatchStatisticsCheck>> ClaimDueAsync(DateTime nowUtc, CancellationToken ct = default)
        {
            var due = await _db.MatchStatisticsChecks.AsNoTracking()
                .Where(c => c.State == "Pending" && c.NextCheckUtc <= nowUtc && (c.LockedUntilUtc == null || c.LockedUntilUtc < nowUtc))
                .OrderBy(c => c.NextCheckUtc).Select(c => c.MatchId).Take(MaxPerCycle).ToListAsync(ct).ConfigureAwait(false);
            if (due.Count == 0) return new List<MatchStatisticsCheck>();
            var until = nowUtc + LockDuration;
            if (_db.Database.IsRelational())
            {
                await _db.MatchStatisticsChecks.Where(c => due.Contains(c.MatchId) && (c.LockedUntilUtc == null || c.LockedUntilUtc < nowUtc))
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.LockedUntilUtc, until).SetProperty(c => c.LockOwner, _owner), ct).ConfigureAwait(false);
                return await _db.MatchStatisticsChecks.Where(c => due.Contains(c.MatchId) && c.LockOwner == _owner).ToListAsync(ct).ConfigureAwait(false);
            }
            var rows = await _db.MatchStatisticsChecks.Where(c => due.Contains(c.MatchId)).ToListAsync(ct).ConfigureAwait(false);
            var mine = rows.Where(c => c.LockedUntilUtc == null || c.LockedUntilUtc < nowUtc).ToList();
            foreach (var c in mine) { c.LockedUntilUtc = until; c.LockOwner = _owner; }
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            return mine;
        }

        public static string FieldsJson(OfficialTeamStatistics s) => JsonSerializer.Serialize(new
        {
            s.BallPossession, s.TotalShots, s.ShotsOnTarget, s.ShotsOffTarget, s.BlockedShots, s.Corners, s.Offsides, s.Fouls,
            s.YellowCards, s.RedCards, s.GoalkeeperSaves, s.TotalPasses, s.AccuratePasses, s.PassAccuracy
        });

        private static string Hash(string v) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(v))).ToLowerInvariant();

        /// <summary>
        /// Taraf başına kanonik yazım. Satır yoksa yazılır. Aynı kaynak değer düzelttiyse güncellenir. Farklı kaynaktan satır
        /// varsa: yeni kaynak daha yüksek öncelikliyse onun değerleri kanonik olur, değilse mevcut korunur; değerler farklıysa
        /// her iki durumda da çelişki deftere yazılır. Aynı gözlem (özet) ikinci kez deftere eklenmez.
        /// </summary>
        public async Task<(int Written, string Decision)> WriteAsync(Match match, string sourceKey, int sourceTier, string? sourceUrl,
            OfficialMatchStatistics stats, DateTime nowUtc, CancellationToken ct)
        {
            var written = 0;
            var decisions = new List<string>();
            foreach (var (side, s, name) in new[] { ("Home", stats.Home, match.HomeTeam?.Name), ("Away", stats.Away, match.AwayTeam?.Name) })
            {
                if (!s.HasAnyMeasurement) { decisions.Add(side + ":Empty"); continue; }
                var json = FieldsJson(s);
                var hash = Hash(sourceKey + "|" + side + "|" + json);
                if (await _db.MatchStatisticObservations.AnyAsync(o => o.MatchId == match.Id && o.SourceKey == sourceKey && o.Side == side && o.ContentHash == hash, ct).ConfigureAwait(false))
                {
                    decisions.Add(side + ":Unchanged");
                    continue;
                }

                var observation = new MatchStatisticObservation
                {
                    MatchId = match.Id, SourceKey = sourceKey, SourceUrl = sourceUrl is { Length: > 400 } ? sourceUrl[..400] : sourceUrl,
                    Side = side, FieldsJson = json, ContentHash = hash, ParserVersion = OfficialParserVersions.For(sourceKey), ObservedAtUtc = nowUtc
                };
                var official = OfficialLineupCollector.ProviderPrefix + sourceKey;
                var row = await _db.MatchTeamStatistics.FirstOrDefaultAsync(x => x.MatchId == match.Id && x.Side == side, ct).ConfigureAwait(false);
                if (row == null)
                {
                    row = new MatchTeamStatistic
                    {
                        MatchId = match.Id, ExternalFixtureId = match.ExternalMatchId ?? string.Empty, Side = side, TeamName = name
                    };
                    Copy(row, s, official, nowUtc);
                    _db.MatchTeamStatistics.Add(row);
                    observation.Decision = "Written";
                    written++;
                }
                else
                {
                    var existingJson = FieldsJson(ToOfficial(row));
                    if (existingJson == json)
                    {
                        observation.Decision = "Unchanged";
                    }
                    else if (row.Source == official)
                    {
                        Copy(row, s, official, nowUtc); // kaynak kendi değerini düzeltti
                        observation.Decision = "Updated";
                        written++;
                    }
                    else
                    {
                        var existingKey = row.Source?.StartsWith(OfficialLineupCollector.ProviderPrefix) == true
                            ? row.Source[OfficialLineupCollector.ProviderPrefix.Length..] : null;
                        var existingTier = existingKey == null ? int.MaxValue : (int)(OfficialSourceRegistry.ByKey(existingKey)?.Tier ?? OfficialSourceTier.LicensedSports);
                        observation.Decision = "ConflictRecorded";
                        observation.ConflictDetail = $"kanonik={row.Source}; yeni={official}";
                        if (sourceTier < existingTier)
                        {
                            Copy(row, s, official, nowUtc);
                            observation.ConflictDetail += "; yüksek öncelikli kaynak uygulandı";
                            written++;
                        }
                        else observation.ConflictDetail += "; mevcut kanonik korundu";
                    }
                }
                _db.MatchStatisticObservations.Add(observation);
                decisions.Add(side + ":" + observation.Decision);
            }
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            return (written, string.Join("/", decisions));
        }

        private static void Copy(MatchTeamStatistic row, OfficialTeamStatistics s, string source, DateTime nowUtc)
        {
            row.BallPossession = s.BallPossession; row.TotalShots = s.TotalShots; row.ShotsOnTarget = s.ShotsOnTarget;
            row.ShotsOffTarget = s.ShotsOffTarget; row.BlockedShots = s.BlockedShots; row.Corners = s.Corners; row.Offsides = s.Offsides;
            row.Fouls = s.Fouls; row.YellowCards = s.YellowCards; row.RedCards = s.RedCards; row.GoalkeeperSaves = s.GoalkeeperSaves;
            row.TotalPasses = s.TotalPasses; row.AccuratePasses = s.AccuratePasses; row.PassAccuracy = s.PassAccuracy;
            row.Source = source; row.FetchedAtUtc = nowUtc;
        }

        public static OfficialTeamStatistics ToOfficial(MatchTeamStatistic r) => new(r.BallPossession, r.TotalShots, r.ShotsOnTarget,
            r.ShotsOffTarget, r.BlockedShots, r.Corners, r.Offsides, r.Fouls, r.YellowCards, r.RedCards, r.GoalkeeperSaves,
            r.TotalPasses, r.AccuratePasses, r.PassAccuracy);
    }
}
