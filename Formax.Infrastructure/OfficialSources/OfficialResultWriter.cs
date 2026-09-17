using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Application.Services.OfficialSources;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.OfficialSources
{
    /// <summary>Yazıcının tek maç kararı.</summary>
    public sealed record ResultWriteOutcome(string Outcome, string? Detail)
    {
        public bool Applied => Outcome == OfficialResultWriter.Applied;
    }

    /// <summary>Maç sonucu yazıldıktan sonra arka plan işlerini (analiz, istatistik) bekletmeden uyandırır.</summary>
    public sealed class PostMatchWorkSignal
    {
        private readonly SemaphoreSlim _signal = new(0, 1);

        public void Notify()
        {
            if (_signal.CurrentCount == 0)
            {
                try { _signal.Release(); } catch (SemaphoreFullException) { }
            }
        }

        /// <summary>Sinyal ya da süre dolana kadar bekler.</summary>
        public async Task WaitAsync(TimeSpan timeout, CancellationToken ct)
        {
            try { await _signal.WaitAsync(timeout, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { }
        }
    }

    /// <summary>
    /// KANONİK SONUÇ YAZICISI — API-Football DEĞİL, yalnız resmî kaynağın kaydından.
    ///
    ///  • Final + iki skor: kaynak "teyit" sunuyorsa (TFF maç sayfası) skor karşılaştırılır; uyuşmazsa sonuç
    ///    KESİNLEŞTİRİLMEZ (VerificationPending).
    ///  • Başka kaynaktan kesinleşmiş FARKLI skor körlemesine ezilmez: Conflict kaydı açılır, resmî skor ≥ 8 dk arayla
    ///    ikinci gözlemde aynı kalırsa uygulanır.
    ///  • Postponed / Cancelled / Abandoned: bitmemiş maça yazılır. Kesin sonuç geri alınmaz; skor 0-0 uydurulmaz.
    /// Sonuç yazılınca: skor aynası, puan durumu, istatistik takvimi ve maç sonu analiz işi tetiklenir.
    /// </summary>
    public sealed class OfficialResultWriter
    {
        public const string Applied = "ResultApplied";
        public const string Unchanged = "ResultUnchanged";
        public const string ConflictRecorded = "ResultConflict";
        public const string VerificationPending = "VerificationPending";
        public const string ConfirmationFetchFailed = "ResultConfirmationFetchFailed";
        public const string StatusApplied = "StatusApplied";
        public const string NotFinal = "NotFinal";

        public static readonly TimeSpan ConflictConfirmationGap = TimeSpan.FromMinutes(8);

        private readonly FormaxDbContext _db;
        private readonly ILogger<OfficialResultWriter> _log;
        private readonly IMatchLiveStatsRepository? _liveStats;
        private readonly ILeagueStandingsService? _standings;
        private readonly PostMatchWorkSignal? _signal;

        public OfficialResultWriter(FormaxDbContext db, ILogger<OfficialResultWriter> log,
            IMatchLiveStatsRepository? liveStats = null, ILeagueStandingsService? standings = null, PostMatchWorkSignal? signal = null)
        {
            _db = db; _log = log; _liveStats = liveStats; _standings = standings; _signal = signal;
        }

        /// <summary>Gözlem içerik özeti — aynı gözlem ikinci kez deftere yazılmaz.</summary>
        public static string ObservationHash(OfficialMatchRecord r, CanonicalResultDecision d)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|",
                r.SourceKey, r.OfficialMatchId, r.Status, r.HomeScore, r.AwayScore, r.HalfTimeHome, r.HalfTimeAway,
                d.ResultDetail, d.PenaltyHome, d.PenaltyAway, r.KickoffUtc?.ToString("O"), r.HomeName, r.AwayName)))).ToLowerInvariant();

        public MatchResultObservation NewObservation(Match tracked, OfficialMatchRecord record, CanonicalResultDecision decision,
            string validation, DateTime nowUtc) => new()
        {
            MatchId = tracked.Id,
            SourceKey = record.SourceKey,
            OfficialStatus = record.Status,
            OfficialHomeScore = decision.HomeScore ?? record.HomeScore,
            OfficialAwayScore = decision.AwayScore ?? record.AwayScore,
            ExistingStatus = tracked.Status,
            ExistingHomeScore = tracked.HomeScore,
            ExistingAwayScore = tracked.AwayScore,
            ExistingSource = tracked.ResultSource,
            ObservedAtUtc = nowUtc,
            SourceMatchId = Trim(record.OfficialMatchId, 80),
            SourceUrl = Trim(record.OfficialUrl, 400),
            SourceHomeName = Trim(record.HomeName, 120),
            SourceAwayName = Trim(record.AwayName, 120),
            SourceKickoffUtc = record.KickoffUtc,
            OfficialHalfTimeHome = decision.HalfTimeHome ?? record.HalfTimeHome,
            OfficialHalfTimeAway = decision.HalfTimeAway ?? record.HalfTimeAway,
            OfficialResultDetail = decision.ResultDetail,
            OfficialPenaltyHome = decision.PenaltyHome,
            OfficialPenaltyAway = decision.PenaltyAway,
            ValidationResult = validation,
            ConflictStatus = "None",
            ParserVersion = OfficialParserVersions.For(record.SourceKey),
            ContentHash = ObservationHash(record, decision),
            Decision = "Observed"
        };

        private static string? Trim(string? s, int max) => s == null ? null : s.Length > max ? s[..max] : s;

        /// <summary>
        /// Uzlaşmayla kabul edilmiş kararı kanonik maça uygular. <paramref name="source"/> teyit sunuyorsa (TFF) önce teyit
        /// okunur. Gözlem satırı bu çağrıda deftere eklenir (karar alanı doldurulmuş olarak).
        /// </summary>
        public async Task<ResultWriteOutcome> ApplyAsync(
            int matchId, IOfficialCompetitionSource source, OfficialMatchRecord record, CanonicalResultDecision decision,
            string roundKey, DateTime utcNow, CancellationToken ct = default)
        {
            var tracked = await _db.Matches.FirstAsync(m => m.Id == matchId, ct).ConfigureAwait(false);
            var isFinished = string.Equals(tracked.Status, MatchStatuses.Finished, StringComparison.OrdinalIgnoreCase);
            var official = OfficialLineupCollector.ProviderPrefix + source.SourceKey;
            var observation = NewObservation(tracked, record, decision, "Accepted", utcNow);

            if (decision.Kind == "Final" && decision.HomeScore is int hs && decision.AwayScore is int aws)
            {
                if (isFinished && tracked.HomeScore == hs && tracked.AwayScore == aws && tracked.ResultSource == official
                    && (tracked.ResultDetail ?? OfficialResultDetails.FullTime) == (decision.ResultDetail ?? OfficialResultDetails.FullTime))
                {
                    await AddObservationIfNewAsync(observation, "Unchanged", ct).ConfigureAwait(false);
                    return new(Unchanged, hs + "-" + aws);
                }

                if (isFinished && tracked.ResultSource != official && (tracked.HomeScore != hs || tracked.AwayScore != aws))
                {
                    var prior = await _db.MatchResultObservations.AsNoTracking()
                        .Where(o => o.MatchId == matchId && o.SourceKey == source.SourceKey && o.Decision == "ConflictRecorded")
                        .OrderByDescending(o => o.ObservedAtUtc).FirstOrDefaultAsync(ct).ConfigureAwait(false);
                    var confirmed = prior != null && prior.OfficialHomeScore == hs && prior.OfficialAwayScore == aws
                                    && utcNow - prior.ObservedAtUtc >= ConflictConfirmationGap;
                    if (!confirmed)
                    {
                        observation.Decision = "ConflictRecorded";
                        observation.ConflictStatus = "Conflict";
                        _db.MatchResultObservations.Add(observation);
                        tracked.ResultVerificationStatus = "Conflict";
                        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
                        _log.LogWarning("[RESULT BOT] {MatchId} sonuç çelişkisi: kayıtlı {EH}-{EA} ({ES}) / resmî {H}-{A} ({Source}) — ikinci gözlem bekleniyor",
                            matchId, tracked.HomeScore, tracked.AwayScore, tracked.ResultSource, hs, aws, official);
                        return new(ConflictRecorded, $"kayıtlı {tracked.HomeScore}-{tracked.AwayScore} / resmî {hs}-{aws}");
                    }
                    observation.Decision = "ConflictResolvedAfterConfirmation";
                    observation.ConflictStatus = "Resolved";
                }
                else observation.Decision = "Applied";

                if (source is IOfficialResultConfirmation confirmation)
                {
                    var c = await confirmation.ConfirmScoreAsync(record,
                        new OfficialRoundContext(roundKey, utcNow, OfficialPurposes.Result, matchId), ct).ConfigureAwait(false);
                    if (!c.Ok) return new(ConfirmationFetchFailed, c.Detail);
                    if (c.Value == null || c.Value.Value.Home != hs || c.Value.Value.Away != aws)
                    {
                        tracked.ResultVerificationStatus = "VerificationPending";
                        await AddObservationIfNewAsync(observation, "VerificationPending", ct, save: false).ConfigureAwait(false);
                        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
                        var pageScore = c.Value == null ? "skor yok" : c.Value.Value.Home + "-" + c.Value.Value.Away;
                        return new(VerificationPending, "liste " + hs + "-" + aws + " / maç sayfası " + pageScore);
                    }
                }

                tracked.Status = MatchStatuses.Finished;
                tracked.HomeScore = hs;
                tracked.AwayScore = aws;
                if (decision.HalfTimeHome.HasValue && decision.HalfTimeAway.HasValue)
                {
                    tracked.HalfTimeHomeScore = decision.HalfTimeHome;
                    tracked.HalfTimeAwayScore = decision.HalfTimeAway;
                }
                tracked.ResultDetail = decision.ResultDetail ?? OfficialResultDetails.FullTime;
                tracked.PenaltyHomeScore = decision.PenaltyHome;
                tracked.PenaltyAwayScore = decision.PenaltyAway;
                tracked.ResultUpdatedAtUtc = utcNow;
                tracked.ResultSource = official;
                tracked.ResultVerificationStatus = "Verified";
                _db.MatchResultObservations.Add(observation);

                // İstatistik takvimi — kesin sonuçla aynı işlemde açılır (restart'ta kaybolmaz).
                if (!await _db.MatchStatisticsChecks.AnyAsync(s => s.MatchId == matchId, ct).ConfigureAwait(false))
                    _db.MatchStatisticsChecks.Add(new MatchStatisticsCheck
                    {
                        MatchId = matchId, LeagueId = tracked.LeagueId, FinalResultAtUtc = utcNow, State = "Pending",
                        NextCheckUtc = OfficialStatisticsSchedule.FirstCheck(utcNow), CreatedAtUtc = utcNow, UpdatedAtUtc = utcNow
                    });
                await _db.SaveChangesAsync(ct).ConfigureAwait(false);

                if (_liveStats != null)
                {
                    try
                    {
                        await _liveStats.UpsertAsync(new MatchLiveStats
                        {
                            MatchId = matchId, HomeScore = hs, AwayScore = aws, Minute = 90, Phase = tracked.ResultDetail, UpdatedAt = utcNow
                        }, ct).ConfigureAwait(false);
                        await _liveStats.SaveChangesAsync(ct).ConfigureAwait(false);
                    }
                    catch (Exception ex) { _log.LogWarning(ex, "[RESULT BOT] {MatchId} skor aynası yazılamadı", matchId); }
                }
                if (_standings != null)
                {
                    try { await _standings.RefreshForSettledMatchAsync(tracked.LeagueId, tracked.MatchDate, ct).ConfigureAwait(false); }
                    catch (Exception ex) { _log.LogWarning(ex, "[RESULT BOT] {MatchId} puan durumu yenilenemedi", matchId); }
                }
                _signal?.Notify();

                _log.LogInformation("[RESULT BOT] {MatchId} resmî sonuç yazıldı: {Home}-{Away} {Detail} ({Source})",
                    matchId, hs, aws, tracked.ResultDetail, official);
                return new(Applied, hs + "-" + aws + " " + tracked.ResultDetail);
            }

            if (isFinished)
            {
                await AddObservationIfNewAsync(observation, "Observed", ct).ConfigureAwait(false);
                return new(Unchanged, "FinalAlreadyRecorded"); // kesin sonuç geri alınmaz
            }

            if (decision.Kind is "Postponed" or "Cancelled" or "Abandoned" && decision.MatchStatus is { } newStatus)
            {
                if (string.Equals(tracked.Status, newStatus, StringComparison.OrdinalIgnoreCase))
                {
                    await AddObservationIfNewAsync(observation, "Observed", ct).ConfigureAwait(false);
                    return new(Unchanged, newStatus);
                }
                tracked.Status = newStatus;
                observation.Decision = "StatusApplied";
                _db.MatchResultObservations.Add(observation);
                await Formax.Infrastructure.Outcomes.PredictionRecomputeQueue.EnqueueAsync(_db, matchId, "StatusChange", official,
                    $"status:{matchId}:{newStatus}:{tracked.MatchDate:yyyyMMddHHmm}", utcNow, ct).ConfigureAwait(false);
                await _db.SaveChangesAsync(ct).ConfigureAwait(false);
                return new(StatusApplied, newStatus);
            }

            await AddObservationIfNewAsync(observation, "Observed", ct).ConfigureAwait(false);
            return new(NotFinal, record.Status);
        }

        /// <summary>Aynı içerik özeti bu maç + kaynak için en son gözlemse deftere yeniden yazılmaz.</summary>
        public async Task AddObservationIfNewAsync(MatchResultObservation observation, string decision, CancellationToken ct, bool save = true)
        {
            var last = await _db.MatchResultObservations.AsNoTracking()
                .Where(o => o.MatchId == observation.MatchId && o.SourceKey == observation.SourceKey)
                .OrderByDescending(o => o.ObservedAtUtc).ThenByDescending(o => o.Id)
                .Select(o => o.ContentHash).FirstOrDefaultAsync(ct).ConfigureAwait(false);
            if (last != null && last == observation.ContentHash) return;
            observation.Decision = decision;
            _db.MatchResultObservations.Add(observation);
            if (save) await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }
}
