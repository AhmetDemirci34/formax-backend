using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.DTOs.Lineup;
using Formax.Application.Interfaces;
using Formax.Application.Services.Matches;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Infrastructure.Providers;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.Lineups
{
    /// <summary>Tek kadro denemesinin sonucu.</summary>
    public enum LineupFetchOutcome
    {
        /// <summary>Sağlayıcı gerçek kadro verdi; DB'ye yazıldı.</summary>
        Released,
        /// <summary>Sağlayıcı GEÇERLİ ama kadrosuz cevap verdi. Başarı DEĞİLDİR; sonraki slot açık.</summary>
        Empty,
        /// <summary>FORMAX bütçe kapısı durdurdu — sağlayıcıya hiç gidilmedi. Kontrol SAYILMAZ.</summary>
        BudgetBlocked,
        /// <summary>Plan/rate-limit/ağ hatası. Başarı ya da kontrol SAYILMAZ; slot açık kalır.</summary>
        ProviderError
    }

    public sealed record LineupFetchResult(
        LineupFetchOutcome Outcome,
        bool FirstRelease,
        LineupNormalizer.Orientation? Orientation,
        string? Detail);

    /// <summary>
    /// RESMÎ KADRO ALIMI — slot kararı, kalıcı defter ve DB yazımı TEK yerde.
    ///
    /// <see cref="BackgroundJobs.LineupIngestionJob"/> bu servisi her turda çağırır; admin
    /// teşhis ucu da aynı <see cref="FetchAndStoreAsync"/> yolunu kullanır. Okuma yolu
    /// (maç detayı) bu servisi ÇAĞIRMAZ: sayfa açılışı sağlayıcıya istek üretmez.
    ///
    /// Uç: <c>fixtures/lineups?fixture={ExternalFixtureId}</c> (sağlayıcıda).
    /// </summary>
    public sealed class LineupIngestionService
    {
        public const string ProviderName = "api-football";

        /// <summary>
        /// Aynı fikstürün iki gerçek denemesi arasındaki en kısa süre. Slot kuralı zaten
        /// slot başına tek istek verir; bu soğuma yalnız eşzamanlı iki sürecin aynı anda
        /// istek atmasını ve engellenen turun dakika dakika tekrarlanmasını önler.
        /// T−15/T−10/T−5 slotları 5 dakika arayla olduğu için 5 dakikadan KISA olmalıdır.
        /// </summary>
        public static readonly TimeSpan PerFixtureCooldown = TimeSpan.FromMinutes(2);

        private readonly IMatchLineupRepository _lineups;
        private readonly IFixtureSyncRepository _ledger;
        private readonly ISportsDataProvider _provider;
        private readonly ILogger<LineupIngestionService> _logger;

        public LineupIngestionService(
            IMatchLineupRepository lineups,
            IFixtureSyncRepository ledger,
            ISportsDataProvider provider,
            ILogger<LineupIngestionService> logger)
        {
            _lineups = lineups;
            _ledger = ledger;
            _provider = provider;
            _logger = logger;
        }

        /// <summary>
        /// BİR TUR — zamanı gelmiş en yakın denenmemiş slotu olan maçlar için birer istek.
        /// Dönüş: bu turda denenen (ya da engellenen) maçlar ve sonuçları.
        /// </summary>
        public async Task<IReadOnlyList<(Match Match, LineupFetchResult Result)>> RunSlotsAsync(
            IEnumerable<Match> candidates, DateTime utcNow, int dailyCap, CancellationToken ct = default)
        {
            var results = new List<(Match, LineupFetchResult)>();

            foreach (var match in candidates)
            {
                ct.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(match.ExternalMatchId)) continue;
                var extId = match.ExternalMatchId!;

                var stored = _lineups.GetByMatchId(match.Id);
                var complete = stored?.HomeLineupsReleased == true && stored?.AwayLineupsReleased == true;

                // SON GERÇEK KONTROL kalıcı başlıktan okunur — restart sıfırlamaz.
                if (!LineupPollSchedule.ShouldPoll(match.MatchDate, utcNow, complete, stored?.LastCheckedAtUtc))
                    continue;

                // ATOMİK REZERVASYON — istek HTTP'den ÖNCE deftere yazılır (günlük tavan dahil).
                if (!_ledger.TryReserveFixtureAttempt(
                        extId, FixtureRefreshPurposes.Lineup, PerFixtureCooldown, dailyCap, utcNow))
                    continue;

                var result = await FetchAndStoreAsync(match, utcNow, ct).ConfigureAwait(false);

                switch (result.Outcome)
                {
                    case LineupFetchOutcome.Released:
                        _ledger.RecordFixtureAttemptOutcome(extId, FixtureRefreshPurposes.Lineup, utcNow, "Applied");
                        break;
                    case LineupFetchOutcome.Empty:
                        _ledger.RecordFixtureAttemptOutcome(extId, FixtureRefreshPurposes.Lineup, utcNow, "NoData");
                        break;
                    case LineupFetchOutcome.BudgetBlocked:
                        // İstek yapılmadı: deneme geri alınır (günlük tavana SAYILMAZ).
                        _ledger.RecordFixtureAttemptBlocked(extId, FixtureRefreshPurposes.Lineup, utcNow, "Blocked:Budget");
                        break;
                    default:
                        // Gerçek istek harcandı (tavana sayılır) ama kontrol sayılmaz: slot açık.
                        _ledger.RecordFixtureAttemptOutcome(extId, FixtureRefreshPurposes.Lineup, utcNow, "ProviderError");
                        break;
                }

                results.Add((match, result));
            }

            await _ledger.SaveChangesAsync(ct).ConfigureAwait(false);
            return results;
        }

        /// <summary>
        /// TEK MAÇ — sağlayıcıya sorar, cevabı sınıflandırır ve kalıcı yazar.
        /// Kısa devre YOKTUR (slot kararı <see cref="RunSlotsAsync"/>'tadır).
        /// </summary>
        public async Task<LineupFetchResult> FetchAndStoreAsync(Match match, DateTime utcNow, CancellationToken ct = default)
        {
            var extId = match.ExternalMatchId!;
            SportsLineupResult? raw;
            try
            {
                raw = await _provider.GetOfficialLineupAsync(extId, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (ApiFootballSportsDataProvider.ApiFootballBudgetBlockedException ex)
            {
                _logger.LogWarning("[LINEUP] {MatchId}/{Fixture} bütçe kapısında durdu: {Detail}", match.Id, extId, ex.Message);
                return new LineupFetchResult(LineupFetchOutcome.BudgetBlocked, false, null, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[LINEUP] {MatchId}/{Fixture} sağlayıcı hatası: {Detail}", match.Id, extId, ex.Message);
                return new LineupFetchResult(LineupFetchOutcome.ProviderError, false, null, ex.Message);
            }

            var header = _lineups.GetByMatchId(match.Id);
            var wasReleased = header?.HomeLineupsReleased == true || header?.AwayLineupsReleased == true;

            if (raw == null || !raw.LineupsAnnounced)
            {
                // GEÇERLİ BOŞ CEVAP: yalnız son kontrol anı yazılır; var olan kadro silinmez.
                await _lineups.MarkCheckedAsync(match.Id, extId, ProviderName, utcNow, ct).ConfigureAwait(false);
                await _lineups.SaveChangesAsync(ct).ConfigureAwait(false);
                _logger.LogInformation("[LINEUP] {MatchId}/{Fixture} sağlayıcı kadroyu henüz iletmedi (T{Remaining:+0;-0} dk).",
                    match.Id, extId, Math.Round((utcNow - match.MatchDate).TotalMinutes));
                return new LineupFetchResult(LineupFetchOutcome.Empty, false, null, null);
            }

            var (oriented, how) = LineupNormalizer.Orient(
                raw, ParseExt(match.HomeTeam?.ExternalTeamId), ParseExt(match.AwayTeam?.ExternalTeamId));
            var home = LineupNormalizer.DistinctSide(oriented.HomeStarters, oriented.HomeBench);
            var away = LineupNormalizer.DistinctSide(oriented.AwayStarters, oriented.AwayBench);

            var lineup = new MatchLineup
            {
                MatchId = match.Id,
                HomeLineupsReleased = home.Starters.Count > 0,
                AwayLineupsReleased = away.Starters.Count > 0,
                HomeFormation = oriented.HomeFormation,
                AwayFormation = oriented.AwayFormation,
                ReleasedAt = utcNow,
                FetchedAt = utcNow,
                ExternalFixtureId = extId,
                HomeTeamExternalId = oriented.HomeTeamExternalId,
                AwayTeamExternalId = oriented.AwayTeamExternalId,
                HomeCoach = oriented.HomeCoach,
                AwayCoach = oriented.AwayCoach,
                Provider = ProviderName,
                LastCheckedAtUtc = utcNow
            };
            await _lineups.UpsertAsync(lineup, ct).ConfigureAwait(false);

            var players = new List<MatchLineupPlayer>();
            players.AddRange(Map(match.Id, "Home", "Starter", home.Starters));
            players.AddRange(Map(match.Id, "Home", "Bench", home.Bench));
            players.AddRange(Map(match.Id, "Away", "Starter", away.Starters));
            players.AddRange(Map(match.Id, "Away", "Bench", away.Bench));
            await _lineups.ReplacePlayersAsync(match.Id, players, ct).ConfigureAwait(false);

            // Tek SaveChanges: başlık + oyuncular birlikte yazılır. Maç detayı kadroyu her
            // istekte DB'den kurar (sunucu tarafında detay DTO önbelleği YOKTUR); bir
            // sonraki detay okuması bu satırları hemen görür.
            await _lineups.SaveChangesAsync(ct).ConfigureAwait(false);

            if (how == LineupNormalizer.Orientation.ProviderOrder)
                _logger.LogWarning("[LINEUP] {MatchId}/{Fixture} taraf takım kimliğiyle doğrulanamadı; sağlayıcı sırası kullanıldı.",
                    match.Id, extId);

            _logger.LogInformation(
                "[LINEUP] {MatchId}/{Fixture} kadro yazıldı: ev {HF} {HS}+{HB}, dep {AF} {AS}+{AB} ({How}).",
                match.Id, extId, oriented.HomeFormation ?? "-", home.Starters.Count, home.Bench.Count,
                oriented.AwayFormation ?? "-", away.Starters.Count, away.Bench.Count, how);

            return new LineupFetchResult(LineupFetchOutcome.Released, !wasReleased, how, null);
        }

        private static int? ParseExt(string? raw) => int.TryParse(raw, out var v) ? v : null;

        private static IEnumerable<MatchLineupPlayer> Map(
            int matchId, string side, string role, IEnumerable<SportsLineupPlayer> source)
            => source.Select(p => new MatchLineupPlayer
            {
                Id = Guid.NewGuid(),
                MatchId = matchId,
                Side = side,
                Role = role,
                ShirtNumber = p.ShirtNumber,
                PlayerName = p.Name,
                Position = p.Position,
                Grid = p.Grid,
                IsCaptain = p.IsCaptain
            });
    }
}
