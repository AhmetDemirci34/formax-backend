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
    public sealed record ResultBotMatchOutcome(int MatchId, string Outcome, string? SourceKey, string? Detail);

    public sealed record ResultBotCycleReport(
        int Enqueued, int Rescheduled, int Claimed, int Applied, IReadOnlyList<string> SourcesRead,
        IReadOnlyList<ResultBotMatchOutcome> Matches);

    /// <summary>
    /// API'SİZ RESMÎ SONUÇ BOTU — kullanıcı sayfayı açmasa da çalışır; API-Football çağrılmaz.
    ///
    /// Tur: (1) başlama saati yaklaşan/geçen maçlar için kalıcı kontrol planı açılır (kickoff +105 dk); (2) zamanı gelen
    /// satırlar DB kilidiyle alınır (aynı maç iki işçide işlenmez; kilit süresi dolunca restart sonrası devralınır);
    /// (3) her doğrulanmış kaynağın maç listesi TUR BAŞINA BİR KEZ okunur (devre kesici açıksa okunmaz); (4) maç kimliği
    /// (takımlar + yön + tarih penceresi) doğrulanır, gözlem deftere yazılır; (5) kaynak önceliği/uzlaşma kararıyla kanonik
    /// sonuç yazılır; (6) bulunmadıysa takvimdeki sonraki kontrol yazılır.
    /// </summary>
    public sealed class OfficialResultBotService
    {
        public static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan LookBack = TimeSpan.FromDays(10);
        public static readonly TimeSpan LookAhead = TimeSpan.FromHours(36);

        private readonly FormaxDbContext _db;
        private readonly IReadOnlyList<IOfficialCompetitionSource> _sources;
        private readonly OfficialDataSourceCatalog _catalog;
        private readonly OfficialResultWriter _writer;
        private readonly IConfiguration _config;
        private readonly ILogger<OfficialResultBotService> _log;
        private readonly string _owner = Guid.NewGuid().ToString("N");

        public OfficialResultBotService(FormaxDbContext db, IEnumerable<IOfficialCompetitionSource> sources,
            OfficialDataSourceCatalog catalog, OfficialResultWriter writer, IConfiguration config, ILogger<OfficialResultBotService> log)
        {
            _db = db; _sources = sources.ToList(); _catalog = catalog; _writer = writer; _config = config; _log = log;
        }

        private int MaxPerCycle => Math.Clamp(_config.GetValue("OfficialSources:ResultBot:MaxMatchesPerCycle", 80), 1, 500);

        /// <summary>
        /// YÖNETİCİ YENİDEN DOĞRULAMA — verilen maçların MEVCUT kontrol satırlarının bir sonraki kontrol zamanını şimdiye çeker ve normal
        /// turu çalıştırır (plan → kilit → kaynak → ayrıştırıcı → kimlik → uzlaşma → yazıcı aynen). Yeni iş mantığı yok, sonuç YAZMAZ;
        /// resmî kaynakla eşleşmeyen maç yine yazılmaz. Kontrol satırı yoksa normal planlama turu açar.
        /// </summary>
        public async Task<ResultBotCycleReport> RecheckNowAsync(IReadOnlyCollection<int> matchIds, DateTime nowUtc, CancellationToken ct = default)
        {
            await EnqueueAsync(nowUtc, ct).ConfigureAwait(false);
            var ids = matchIds.ToList();
            var rows = await _db.MatchResultChecks.Where(c => ids.Contains(c.MatchId)).ToListAsync(ct).ConfigureAwait(false);
            foreach (var c in rows.Where(c => c.State is "Pending" or "NoOfficialSource" or "Postponed"))
            {
                c.NextCheckUtc = nowUtc;
                c.UpdatedAtUtc = nowUtc;
            }
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            return await RunCycleAsync(nowUtc, ct).ConfigureAwait(false);
        }

        public async Task<ResultBotCycleReport> RunCycleAsync(DateTime nowUtc, CancellationToken ct = default)
        {
            await _catalog.EnsureSeededAsync(nowUtc, ct).ConfigureAwait(false);
            var (enqueued, rescheduled) = await EnqueueAsync(nowUtc, ct).ConfigureAwait(false);
            var claimed = await ClaimDueAsync(nowUtc, ct).ConfigureAwait(false);
            if (claimed.Count == 0) return new(enqueued, rescheduled, 0, 0, Array.Empty<string>(), Array.Empty<ResultBotMatchOutcome>());

            var roundKey = $"resultbot:{nowUtc:yyyyMMddHHmm}";
            var ids = claimed.Select(c => c.MatchId).ToList();
            var matches = await _db.Matches.AsNoTracking().Include(m => m.HomeTeam).Include(m => m.AwayTeam)
                .Where(m => ids.Contains(m.Id)).ToDictionaryAsync(m => m.Id, ct).ConfigureAwait(false);

            // Kaynak başına tur tekilliği: aynı kaynağın listesi bu turda bir kez okunur.
            var feeds = new Dictionary<string, OfficialRead<IReadOnlyList<OfficialMatchRecord>>?>(StringComparer.Ordinal);
            var sourcesRead = new List<string>();
            var outcomes = new List<ResultBotMatchOutcome>();
            var applied = 0;

            foreach (var check in claimed)
            {
                ct.ThrowIfCancellationRequested();
                if (!matches.TryGetValue(check.MatchId, out var match))
                {
                    check.State = "Resolved"; check.LastOutcome = "MatchMissing"; Release(check, nowUtc);
                    continue;
                }
                if (string.Equals(match.Status, MatchStatuses.Finished, StringComparison.OrdinalIgnoreCase) && match.ResultSource?.StartsWith("official:") == true)
                {
                    check.State = "Resolved"; check.ResolvedStatus ??= match.ResultDetail ?? OfficialResultDetails.FullTime;
                    check.ResolvedAtUtc ??= match.ResultUpdatedAtUtc ?? nowUtc; check.LastOutcome = "AlreadyFinal"; Release(check, nowUtc);
                    continue;
                }

                var descriptors = OfficialSourceRegistry.VerifiedFor(match.LeagueId, OfficialPurposes.Result);
                var usable = descriptors.Select(d => (d, src: _sources.FirstOrDefault(s => s.SourceKey == d.Key))).Where(x => x.src != null).ToList();
                if (usable.Count == 0)
                {
                    // Kaynak yok: eski NotStarted durumu "doğru" sayılmaz — teşhiste ResultSourceUnavailable görünür.
                    check.State = "NoOfficialSource";
                    check.LastOutcome = "ResultSourceUnavailable";
                    (check.LastErrorClass, check.LastValidationStatus) = ResultAttemptClassifier.Classify("ResultSourceUnavailable", null);
                    check.AttemptCount++;
                    check.NextCheckUtc = nowUtc.AddHours(24);
                    Release(check, nowUtc);
                    outcomes.Add(new(match.Id, "ResultSourceUnavailable", null, null));
                    continue;
                }

                var observations = new List<(SourceResultObservation Obs, IOfficialCompetitionSource Src, OfficialMatchRecord Rec)>();
                string? lastFailure = null;
                foreach (var (d, src) in usable)
                {
                    if (!feeds.TryGetValue(d.Key, out var feed))
                    {
                        var health = await _catalog.GetAsync(d.Key, ct).ConfigureAwait(false);
                        if (OfficialDataSourceCatalog.IsCircuitOpen(health, nowUtc))
                        {
                            feed = null;
                            sourcesRead.Add(d.Key + ":CircuitOpen");
                        }
                        else
                        {
                            try
                            {
                                feed = await src!.ReadMatchesAsync(new OfficialRoundContext(roundKey, nowUtc, OfficialPurposes.Result), ct).ConfigureAwait(false);
                            }
                            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                            catch (Exception ex)
                            {
                                // Parser/kaynak bozulması bütün servisi çökertmez: sağlık kaydına yazılır, diğer kaynaklara devam edilir.
                                feed = new OfficialRead<IReadOnlyList<OfficialMatchRecord>>(null, OfficialReadOutcomes.ParseFailed, ex.GetType().Name + ": " + ex.Message, null);
                            }
                            await _catalog.RecordReadAsync(d.Key, feed, nowUtc, ct).ConfigureAwait(false);
                            sourcesRead.Add(d.Key + ":" + feed.Outcome);
                        }
                        feeds[d.Key] = feed;
                    }
                    if (feed == null) { lastFailure = d.Key + ":CircuitOpen"; continue; }
                    if (!feed.Ok || feed.Value == null) { lastFailure = d.Key + ":" + feed.Outcome + ":" + feed.Detail; continue; }

                    var (record, validation) = await ResolveRecordAsync(match, d.Key, feed.Value, nowUtc, ct).ConfigureAwait(false);
                    if (record == null)
                    {
                        lastFailure = d.Key + ":" + validation;
                        continue;
                    }
                    var decision = OfficialResultStatusPolicy.Decide(record);
                    if (decision.Kind == "Final" && check.FirstFinalSeenUtc == null) check.FirstFinalSeenUtc = nowUtc;
                    if (decision.Kind == "Final" && check.SourcePublishedFinalAtUtc == null
                        && record.Extra?.GetValueOrDefault("sourcePublishedAtUtc") is { } published
                        && DateTime.TryParse(published, System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var publishedUtc))
                        check.SourcePublishedFinalAtUtc = DateTime.SpecifyKind(publishedUtc, DateTimeKind.Utc);
                    if (decision.Kind == "NotFinal") check.LastNotFinalCheckUtc = nowUtc;
                    observations.Add((new SourceResultObservation(d.Key, d.Tier, decision), src!, record));
                }

                var consensus = ResultConsensusPolicy.Decide(observations.Select(o => o.Obs).ToList());
                string outcome;
                string? detail = consensus.Reason;
                string? usedSource = observations.FirstOrDefault().Obs?.SourceKey;
                if (consensus.Outcome == ResultConsensusPolicy.Accept && consensus.Accepted != null)
                {
                    var chosen = observations.First(o => o.Obs.Decision == consensus.Accepted);
                    usedSource = chosen.Obs.SourceKey;
                    var w = await _writer.ApplyAsync(match.Id, chosen.Src, chosen.Rec, consensus.Accepted, roundKey, nowUtc, ct).ConfigureAwait(false);
                    outcome = w.Outcome;
                    detail = w.Detail;
                    if (w.Applied || w.Outcome == OfficialResultWriter.Unchanged && consensus.Accepted.Kind == "Final")
                    {
                        applied += w.Applied ? 1 : 0;
                        check.State = "Resolved";
                        check.ResolvedStatus = consensus.Accepted.ResultDetail;
                        check.ResolvedAtUtc ??= nowUtc;
                    }
                    else if (w.Outcome == OfficialResultWriter.StatusApplied || (w.Outcome == OfficialResultWriter.Unchanged && consensus.Accepted.Kind != "Final"))
                    {
                        check.State = consensus.Accepted.Kind; // Postponed | Cancelled | Abandoned
                        check.ResolvedStatus = consensus.Accepted.Kind;
                        check.ResolvedAtUtc ??= nowUtc;
                    }
                }
                else
                {
                    // Gözlemler (bitmemiş ya da çelişkili) defterde; kanonik yazılmaz.
                    foreach (var o in observations)
                    {
                        var obs = _writer.NewObservation(match, o.Rec, o.Obs.Decision, "Accepted", nowUtc);
                        if (consensus.Outcome == ResultConsensusPolicy.Conflict) obs.ConflictStatus = "Conflict";
                        await _writer.AddObservationIfNewAsync(obs, consensus.Outcome == ResultConsensusPolicy.Conflict ? "ConflictRecorded" : "Observed", ct).ConfigureAwait(false);
                    }
                    outcome = consensus.Outcome == ResultConsensusPolicy.Conflict ? "Conflict" : observations.Count == 0 ? "NoObservation" : "NotFinalYet";
                    if (observations.Count == 0) detail = lastFailure;
                    if (consensus.Outcome == ResultConsensusPolicy.Conflict)
                    {
                        var tracked = await _db.Matches.FirstAsync(m => m.Id == match.Id, ct).ConfigureAwait(false);
                        tracked.ResultVerificationStatus = "Conflict";
                    }
                }

                check.AttemptCount++;
                check.LastOutcome = Trim(outcome + (detail == null ? "" : ":" + detail), 120);
                check.LastSourceKey = usedSource ?? lastFailure?.Split(':')[0];
                (check.LastErrorClass, check.LastValidationStatus) = ResultAttemptClassifier.Classify(outcome, lastFailure);
                if (check.State is "Pending" or "NoOfficialSource" or "Postponed")
                {
                    check.State = check.State == "Postponed" ? "Postponed" : "Pending";
                    check.NextCheckUtc = check.State == "Postponed"
                        ? nowUtc.AddHours(24)
                        : OfficialResultSchedule.NextCheck(check.KickoffUtc, check.AttemptCount, nowUtc);
                    // Kayıtlı skorla çelişen resmî gözlem ikinci gözlemle teyit edilir (≥ ConflictConfirmationGap): geri çekilme bunu
                    // ertesi güne atmaz; tek seferlik kısa tekrar, teyitte sonuç yazılır ve plan kapanır.
                    if (check.LastErrorClass == "Conflict")
                    {
                        var confirm = nowUtc + OfficialResultWriter.ConflictConfirmationGap + TimeSpan.FromMinutes(1);
                        if (confirm < check.NextCheckUtc) check.NextCheckUtc = confirm;
                    }
                }
                Release(check, nowUtc);
                outcomes.Add(new(match.Id, outcome, usedSource, detail));
            }

            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            if (outcomes.Count > 0)
                _log.LogInformation("[RESULT BOT] tur {Round}: plan+{Enq} yeniden={Res} işlenen={Claimed} yazılan={Applied} kaynak=[{Sources}] {Outcomes}",
                    roundKey, enqueued, rescheduled, claimed.Count, applied, string.Join(",", sourcesRead),
                    string.Join(", ", outcomes.Select(o => $"{o.MatchId}:{o.Outcome}")));
            return new(enqueued, rescheduled, claimed.Count, applied, sourcesRead, outcomes);
        }

        private static void Release(MatchResultCheck check, DateTime nowUtc)
        {
            check.LastCheckUtc = nowUtc;
            check.UpdatedAtUtc = nowUtc;
            check.LockOwner = null;
            check.LockedUntilUtc = null;
        }

        private static string Trim(string s, int max) => s.Length > max ? s[..max] : s;

        /// <summary>
        /// PLANLAMA — kilitli organizasyonlarda başlama saati [şimdi−10 gün, şimdi+36 sa] aralığındaki bitmemiş maçlar için
        /// kontrol satırı açılır (ilk kontrol kickoff+105). Başlama saati değişmiş maçın planı yeni saate göre sıfırlanır.
        /// </summary>
        public async Task<(int Enqueued, int Rescheduled)> EnqueueAsync(DateTime nowUtc, CancellationToken ct = default)
        {
            var allow = CoveragePolicy.LeagueAllowList(_config);
            var locked = LockedCompetitions.All.Where(l => CoveragePolicy.Allows(allow, l)).ToList();
            var from = nowUtc - LookBack;
            var to = nowUtc + LookAhead;

            var candidates = await _db.Matches.AsNoTracking()
                .Where(m => locked.Contains(m.LeagueId) && m.MatchDate >= from && m.MatchDate <= to
                            && (m.Status == MatchStatuses.NotStarted || m.Status == MatchStatuses.Live || m.Status == MatchStatuses.PreMatch
                                || m.Status == MatchStatuses.Postponed
                                // Resmî olmayan kaynaktan (eski API-Football) "bitti" yazılmış maç da resmî kaynakla uzlaştırılır.
                                || (m.Status == MatchStatuses.Finished && (m.ResultSource == null || !m.ResultSource.StartsWith("official:"))))
                            && !_db.MatchResultChecks.Any(c => c.MatchId == m.Id))
                .OrderBy(m => m.MatchDate).Take(500)
                .Select(m => new { m.Id, m.LeagueId, m.MatchDate })
                .ToListAsync(ct).ConfigureAwait(false);
            foreach (var m in candidates)
                _db.MatchResultChecks.Add(new MatchResultCheck
                {
                    MatchId = m.Id, LeagueId = m.LeagueId, KickoffUtc = m.MatchDate, State = "Pending",
                    NextCheckUtc = OfficialResultSchedule.FirstCheck(m.MatchDate), CreatedAtUtc = nowUtc, UpdatedAtUtc = nowUtc
                });

            // Başlama saati değişmiş (erteleme sonrası yeni tarih, öne çekilen maç) satırlar — plan sıfırlanır.
            var moved = await (from c in _db.MatchResultChecks
                               join m in _db.Matches on c.MatchId equals m.Id
                               where (c.State == "Pending" || c.State == "Postponed" || c.State == "NoOfficialSource")
                                     && c.KickoffUtc != m.MatchDate && m.Status != MatchStatuses.Finished
                               select new { Check = c, m.MatchDate, m.Status }).Take(200).ToListAsync(ct).ConfigureAwait(false);
            foreach (var x in moved)
            {
                x.Check.KickoffUtc = x.MatchDate;
                x.Check.AttemptCount = 0;
                x.Check.State = "Pending";
                x.Check.NextCheckUtc = OfficialResultSchedule.FirstCheck(x.MatchDate);
                x.Check.UpdatedAtUtc = nowUtc;
            }
            // Kaynak sonradan doğrulandıysa (ör. 17.09.2026 UEFA) "kaynak yok" diye 24 saate ertelenmiş kontroller HEMEN yeniden açılır.
            var verifiedLeagues = locked.Where(l => OfficialSourceRegistry.VerifiedFor(l, OfficialPurposes.Result).Count > 0).ToList();
            var reopened = await _db.MatchResultChecks
                .Where(c => c.State == "NoOfficialSource" && verifiedLeagues.Contains(c.LeagueId) && c.NextCheckUtc > nowUtc)
                .Take(500).ToListAsync(ct).ConfigureAwait(false);
            foreach (var c in reopened)
            {
                c.State = "Pending";
                c.NextCheckUtc = nowUtc;
                c.UpdatedAtUtc = nowUtc;
            }
            if (candidates.Count > 0 || moved.Count > 0 || reopened.Count > 0) await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            return (candidates.Count, moved.Count + reopened.Count);
        }

        /// <summary>Zamanı gelen satırları DB kilidiyle alır; başka işçinin geçerli kilidindeki satır alınmaz.</summary>
        public async Task<List<MatchResultCheck>> ClaimDueAsync(DateTime nowUtc, CancellationToken ct = default)
        {
            var dueIds = await _db.MatchResultChecks.AsNoTracking()
                .Where(c => (c.State == "Pending" || c.State == "NoOfficialSource" || c.State == "Postponed")
                            && c.NextCheckUtc <= nowUtc && (c.LockedUntilUtc == null || c.LockedUntilUtc < nowUtc))
                .OrderBy(c => c.NextCheckUtc).Select(c => c.MatchId).Take(MaxPerCycle)
                .ToListAsync(ct).ConfigureAwait(false);
            if (dueIds.Count == 0) return new List<MatchResultCheck>();

            var until = nowUtc + LockDuration;
            if (_db.Database.IsRelational())
            {
                await _db.MatchResultChecks
                    .Where(c => dueIds.Contains(c.MatchId) && (c.LockedUntilUtc == null || c.LockedUntilUtc < nowUtc))
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.LockedUntilUtc, until).SetProperty(c => c.LockOwner, _owner), ct)
                    .ConfigureAwait(false);
                return await _db.MatchResultChecks.Where(c => dueIds.Contains(c.MatchId) && c.LockOwner == _owner)
                    .ToListAsync(ct).ConfigureAwait(false);
            }

            // Test sağlayıcısı (InMemory): aynı koşul izlenen varlıkla uygulanır.
            var rows = await _db.MatchResultChecks.Where(c => dueIds.Contains(c.MatchId)).ToListAsync(ct).ConfigureAwait(false);
            var mine = rows.Where(c => c.LockedUntilUtc == null || c.LockedUntilUtc < nowUtc).ToList();
            foreach (var c in mine) { c.LockedUntilUtc = until; c.LockOwner = _owner; }
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            return mine;
        }

        /// <summary>
        /// KİMLİK — kayıtlı bağlantı varsa kaynak kimliğiyle bulunur ve yön yeniden sınanır; yoksa takımlar + yön + tarih
        /// penceresiyle çözülür ve bağlantı yazılır. Yanlış takım / yanlış tarih / ters yön reddedilir.
        /// </summary>
        public async Task<(OfficialMatchRecord? Record, string Validation)> ResolveRecordAsync(
            Match match, string sourceKey, IReadOnlyList<OfficialMatchRecord> feed, DateTime nowUtc, CancellationToken ct)
        {
            var home = match.HomeTeam?.Name ?? string.Empty;
            var away = match.AwayTeam?.Name ?? string.Empty;
            var link = await _db.OfficialMatchLinks.FirstOrDefaultAsync(l => l.MatchId == match.Id && l.SourceKey == sourceKey, ct).ConfigureAwait(false);
            if (link != null)
            {
                var byId = feed.FirstOrDefault(r => r.OfficialMatchId == link.OfficialMatchId);
                if (byId != null)
                {
                    if (!(OfficialMatchIdentityResolver.HomeMatches(byId, home) && OfficialMatchIdentityResolver.AwayMatches(byId, away)))
                        return (null, "OrientationMismatch");
                    return (byId, "Accepted");
                }
            }

            var decision = OfficialMatchIdentityResolver.Resolve(new FormaxMatchIdentity(match.Id, match.LeagueId, home, away, match.MatchDate), feed);
            if (!decision.Accepted)
                return (null, decision.Reason switch
                {
                    OfficialIdentityDecision.ReasonKickoffOutOfWindow => "WrongDate",
                    OfficialIdentityDecision.ReasonOrientationReversed => "OrientationMismatch",
                    OfficialIdentityDecision.ReasonNoCandidate => "NoCandidate",
                    _ => decision.Reason
                });

            var record = decision.Record!;
            if (link == null && !await _db.OfficialMatchLinks.AnyAsync(l => l.SourceKey == sourceKey && l.OfficialMatchId == record.OfficialMatchId, ct).ConfigureAwait(false))
            {
                _db.OfficialMatchLinks.Add(new OfficialMatchLink
                {
                    MatchId = match.Id, SourceKey = sourceKey, OfficialMatchId = record.OfficialMatchId, OfficialUrl = record.OfficialUrl,
                    OfficialHomeName = record.HomeName, OfficialAwayName = record.AwayName, OfficialKickoffUtc = record.KickoffUtc,
                    OfficialStatus = record.Status, LinkedAtUtc = nowUtc, VerifiedAtUtc = nowUtc
                });
                await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            }
            return (record, "Accepted");
        }
    }
}
