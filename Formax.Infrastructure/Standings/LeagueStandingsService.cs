using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.DTOs.Standings;
using Formax.Application.Interfaces;
using Formax.Application.Services.Standings;
using Formax.Domain.Entities;
using Formax.Infrastructure.BackgroundJobs;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Seasons;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.Standings
{
    /// <summary>
    /// <see cref="ILeagueStandingsService"/> uygulaması — projeksiyon + snapshot + cache.
    ///
    /// KOTA ETKİSİ SIFIR: hiçbir sağlayıcıya (api-football dâhil) istek gitmez, hiçbir
    /// sayfa kazınmaz. Girdi yalnız kendi Matches tablomuzdur.
    /// </summary>
    public sealed class LeagueStandingsService : ILeagueStandingsService
    {
        /// <summary>Bu yaştan eski snapshot "güncel" sayılmaz → UI "güncelleniyor" der.</summary>
        public static readonly TimeSpan FreshnessWindow = TimeSpan.FromHours(2);

        /// <summary>Cache ömrü — okuma yolu bu süre boyunca DB'ye bile gitmez.</summary>
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

        // Lig+sezon başına tek hesap: eşzamanlı ilk okumalar aynı hesabı bekler
        // (kullanıcı sayısı hesaplama sayısını ARTIRMAZ).
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new();

        private readonly FormaxDbContext _db;
        private readonly IMatchReadRepository _matches;
        private readonly ILeagueSeasonResolver _seasons;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _config;
        private readonly ILogger<LeagueStandingsService> _log;

        public LeagueStandingsService(
            FormaxDbContext db,
            IMatchReadRepository matches,
            ILeagueSeasonResolver seasons,
            IMemoryCache cache,
            IConfiguration config,
            ILogger<LeagueStandingsService> log)
        {
            _db = db;
            _matches = matches;
            _seasons = seasons;
            _cache = cache;
            _config = config;
            _log = log;
        }

        private static string KeyOf(int leagueId, int seasonYear) => $"standings:{leagueId}:{seasonYear}";

        public LeagueStandingsSnapshotDto? GetCached(int leagueId, int seasonId)
        {
            var key = KeyOf(leagueId, seasonId);
            if (_cache.TryGetValue(key, out LeagueStandingsSnapshotDto? cached) && cached != null)
                return WithFreshness(cached);

            var stored = _db.LeagueStandingsSnapshots.AsNoTracking()
                .FirstOrDefault(s => s.LeagueId == leagueId && s.SeasonYear == seasonId);
            if (stored == null) return null;   // HESAP YOK: snapshot yoksa çağıran fallback kullanır

            var dto = ToDto(stored, ResolveLeagueName(leagueId));
            _cache.Set(key, dto, CacheTtl);
            return WithFreshness(dto);
        }

        public async Task<LeagueStandingsSnapshotDto?> GetAsync(int leagueId, int seasonId, CancellationToken ct = default)
        {
            var key = KeyOf(leagueId, seasonId);

            if (_cache.TryGetValue(key, out LeagueStandingsSnapshotDto? cached) && cached != null)
                return WithFreshness(cached);

            var stored = await _db.LeagueStandingsSnapshots.AsNoTracking()
                .FirstOrDefaultAsync(s => s.LeagueId == leagueId && s.SeasonYear == seasonId, ct)
                .ConfigureAwait(false);

            if (stored != null)
            {
                var dto = ToDto(stored, ResolveLeagueName(leagueId));
                _cache.Set(key, dto, CacheTtl);
                return WithFreshness(dto);
            }

            // Hiç snapshot yok → ilk okuma bir kez üretir. Eşzamanlı istekler tek hesabı bekler.
            var gate = Locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (_cache.TryGetValue(key, out LeagueStandingsSnapshotDto? afterWait) && afterWait != null)
                    return WithFreshness(afterWait);

                return await RefreshAsync(leagueId, seasonId, ct).ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        }

        public async Task<LeagueStandingsSnapshotDto?> RefreshAsync(int leagueId, int seasonYear, CancellationToken ct = default)
        {
            var resolution = _seasons.Resolve(leagueId, new DateTime(seasonYear, 8, 1, 0, 0, 0, DateTimeKind.Utc));
            if (!resolution.Resolved)
            {
                _log.LogWarning("Standings projection skipped — {Error}", resolution.Error);

                // DOĞRULANMAMIŞ KAYIT BIRAKILMAZ: sezon metadata kaydı olmadan üretilmiş
                // eski bir snapshot varsa kaldırılır. Kaynağı doğrulanmamış bir tablo
                // kullanıcıya "puan durumu" diye gösterilemez.
                var orphan = await _db.LeagueStandingsSnapshots
                    .FirstOrDefaultAsync(s => s.LeagueId == leagueId && s.SeasonYear == seasonYear, ct)
                    .ConfigureAwait(false);
                if (orphan != null)
                {
                    _db.LeagueStandingsSnapshots.Remove(orphan);
                    await _db.SaveChangesAsync(ct).ConfigureAwait(false);
                    _log.LogWarning(
                        "Removed unverified standings snapshot — league {LeagueId} season {Season}.",
                        leagueId, seasonYear);
                }
                _cache.Remove(KeyOf(leagueId, seasonYear));

                return null;
            }

            var scope = resolution.Scope!;
            var settledAll = _matches.GetSettledLeagueMatchesInSeason(leagueId, scope.StartUtc, scope.EndUtc);

            // ── AŞAMA SÜZGECİ (01.09.2026) ───────────────────────────────────────
            // UCL/UEL/UECL NORMAL LİG DEĞİLDİR. Eleme turlarının çift maçlı sonuçlarını
            // toplayıp puan tablosu üretmek YASAKTIR: ortaya gerçek olmayan bir sıralama
            // çıkar. Tabloya YALNIZ lig aşaması maçları girer; aşaması çözülemeyen maç
            // (Round boş/tanınmıyor) lig aşaması SAYILMAZ.
            var isUefa = Formax.Domain.Constants.LockedCompetitions.IsUefa(leagueId);
            var settled = settledAll;
            var unresolvedPhase = 0;
            var scopePhase = isUefa ? "LeaguePhase" : "DomesticLeague";

            if (isUefa)
            {
                settled = settledAll
                    .Where(m => CompetitionPhaseResolver.Resolve(leagueId, m.Round)
                                == CompetitionPhase.LeaguePhase)
                    .ToList();
                unresolvedPhase = settledAll.Count(m =>
                    CompetitionPhaseResolver.Resolve(leagueId, m.Round) == CompetitionPhase.Unknown);

                if (settled.Count == 0) scopePhase = "None";

                _log.LogInformation(
                    "UEFA standings scope — league {LeagueId} season {Season}: {Kept}/{Total} maç lig aşaması " +
                    "({Unresolved} maçın aşaması çözülemedi, tabloya ALINMADI).",
                    leagueId, seasonYear, settled.Count, settledAll.Count, unresolvedPhase);
            }

            var projection = StandingsProjector.Project(leagueId, settled);

            // VERİ TAMLIĞI: bu tarihe kadar oynanmış OLMASI GEREKEN maçların kaçı kesinleşti?
            // UEFA'da bu soru yalnız LİG AŞAMASI için sorulur.
            var now = DateTime.UtcNow;
            var fixtures = _matches.GetSeasonLeagueFixturesBefore(leagueId, scope.StartUtc, scope.EndUtc, now);
            var completeness = SeasonDataCompleteness.EvaluateForStandings(leagueId, fixtures, now);
            if (!completeness.IsComplete)
            {
                _log.LogWarning(
                    "Standings incomplete — league {LeagueId} season {Season}: {Missing} of {Expected} fixtures unsettled ({Ids}).",
                    leagueId, seasonYear, completeness.Missing, completeness.Expected,
                    string.Join(",", completeness.MissingMatchIds.Take(10)));
            }

            var entity = await _db.LeagueStandingsSnapshots
                .FirstOrDefaultAsync(s => s.LeagueId == leagueId && s.SeasonYear == seasonYear, ct)
                .ConfigureAwait(false);

            var isNew = entity == null;
            entity ??= new LeagueStandingsSnapshot { LeagueId = leagueId, SeasonYear = seasonYear };

            entity.SeasonStartUtc = scope.StartUtc;
            entity.CalculatedAtUtc = DateTime.UtcNow;
            entity.LastIncludedMatchUtc = projection.LastIncludedMatchUtc;
            entity.Source = StandingsSnapshotSources.InternalResultsProjection;
            entity.MatchesIncluded = projection.MatchesIncluded;
            entity.RankingRuleId = projection.RankingRuleId;
            entity.IsProvisional = projection.IsProvisional;
            entity.RowsJson = JsonSerializer.Serialize(projection.Rows);
            entity.ExpectedCompletedFixtures = completeness.Expected;
            entity.IncludedCompletedFixtures = completeness.Included;
            entity.MissingCompletedFixtures = completeness.Missing;
            entity.IsComplete = completeness.IsComplete;
            entity.CompletenessCheckedAtUtc = completeness.CheckedAtUtc;
            entity.PostponedFixtures = completeness.Postponed;
            entity.CancelledFixtures = completeness.Cancelled;
            entity.AbandonedFixtures = completeness.Abandoned;
            entity.StaleResultFixtures = completeness.StaleResult;
            entity.ScopePhase = scopePhase;
            entity.UnresolvedPhaseFixtures = unresolvedPhase;
            // Ulusal ligde aşama sorusu yoktur. UEFA'da tabloya giren maç yoksa ama aşaması
            // çözülemeyen maç VARSA, sessizce "tablo yok" demek yanıltıcı olur: çözülemeyen
            // aşama tanı koduyla taşınır ve okuma yolu tablo göstermez.
            entity.PhaseResolution = !isUefa
                ? "Resolved"
                : (settled.Count == 0 && unresolvedPhase > 0
                    ? CompetitionPhaseResolver.PhaseUnresolvedCode
                    : "Resolved");

            if (isNew) _db.LeagueStandingsSnapshots.Add(entity);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);

            var dto = ToDto(entity, ResolveLeagueName(leagueId));
            _cache.Set(KeyOf(leagueId, seasonYear), dto, CacheTtl);
            return WithFreshness(dto);
        }

        public async Task<int> RefreshCurrentSeasonAsync(CancellationToken ct = default)
        {
            var seasonYear = _seasons.SeasonYearOf(DateTime.UtcNow);
            var refreshed = 0;

            foreach (var leagueId in ScopeLeagues())
            {
                ct.ThrowIfCancellationRequested();
                var dto = await RefreshAsync(leagueId, seasonYear, ct).ConfigureAwait(false);
                if (dto != null) refreshed++;
            }

            return refreshed;
        }

        public async Task RefreshForSettledMatchAsync(int leagueId, DateTime matchDateUtc, CancellationToken ct = default)
        {
            var seasonYear = _seasons.SeasonYearOf(matchDateUtc);
            LeagueSeasonResolver.Invalidate(leagueId, seasonYear);
            await RefreshAsync(leagueId, seasonYear, ct).ConfigureAwait(false);
        }

        /// <summary>Kapsamdaki ligler — okuma tarafıyla AYNI allow-list.</summary>
        private IEnumerable<int> ScopeLeagues()
        {
            var allow = CoveragePolicy.LeagueAllowList(_config);
            if (allow.Count > 0) return allow;

            // Allow-list boşsa depodaki gerçek lig id'leri kullanılır (uydurma yok).
            return _db.Matches.AsNoTracking()
                .Select(m => m.LeagueId)
                .Distinct()
                .ToList();
        }

        private string ResolveLeagueName(int leagueId)
            => _db.Matches.AsNoTracking()
                   .Where(m => m.LeagueId == leagueId && m.League != null && m.League != "")
                   .Select(m => m.League)
                   .FirstOrDefault() ?? string.Empty;

        private static LeagueStandingsSnapshotDto ToDto(LeagueStandingsSnapshot e, string leagueName)
        {
            List<LeagueStandingsRowDto> rows;
            try
            {
                rows = JsonSerializer.Deserialize<List<LeagueStandingsRowDto>>(e.RowsJson) ?? new();
            }
            catch (JsonException)
            {
                rows = new();
            }

            return new LeagueStandingsSnapshotDto
            {
                LeagueId = e.LeagueId,
                LeagueName = leagueName,
                SeasonId = e.SeasonYear,
                SeasonLabel = $"{e.SeasonYear}/{(e.SeasonYear + 1) % 100:00}",
                SeasonStartDate = e.SeasonStartUtc,
                CalculatedAtUtc = e.CalculatedAtUtc,
                LastIncludedMatchUtc = e.LastIncludedMatchUtc,
                Source = e.Source,
                MatchesIncluded = e.MatchesIncluded,
                RankingRuleId = e.RankingRuleId,
                IsProvisional = e.IsProvisional,
                ExpectedCompletedFixtures = e.ExpectedCompletedFixtures,
                IncludedCompletedFixtures = e.IncludedCompletedFixtures,
                MissingCompletedFixtures = e.MissingCompletedFixtures,
                IsComplete = e.IsComplete,
                CompletenessCheckedAtUtc = e.CompletenessCheckedAtUtc,
                PostponedFixtures = e.PostponedFixtures,
                CancelledFixtures = e.CancelledFixtures,
                AbandonedFixtures = e.AbandonedFixtures,
                StaleResultFixtures = e.StaleResultFixtures,
                ScopePhase = e.ScopePhase,
                PhaseResolution = e.PhaseResolution,
                UnresolvedPhaseFixtures = e.UnresolvedPhaseFixtures,
                Rows = rows
            };
        }

        /// <summary>Tazelik OKUMA anında belirlenir — snapshot'ta donmuş bir bayrak taşınmaz.</summary>
        private static LeagueStandingsSnapshotDto WithFreshness(LeagueStandingsSnapshotDto dto)
        {
            dto.IsFresh = DateTime.UtcNow - dto.CalculatedAtUtc <= FreshnessWindow;
            return dto;
        }
    }
}
