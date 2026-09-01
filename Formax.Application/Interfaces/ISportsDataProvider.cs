using Formax.Application.DTOs.Diagnostics;
using Formax.Application.DTOs.Fixtures;
using Formax.Application.DTOs.Lineup;
using Formax.Application.DTOs.Live;
using Formax.Application.DTOs.Odds;
using Formax.Application.DTOs.Players;
using Formax.Application.DTOs.Predictions;
using Formax.Application.DTOs.Standings;

namespace Formax.Application.Interfaces
{
    /// <summary>
    /// Abstraction over an external sports data API.
    /// Implementations are vendor-specific (api-football, etc.).
    /// </summary>
    public interface ISportsDataProvider
    {
        // ── Sprint 0: Fixture sync ────────────────────────────────────────────

        /// <summary>
        /// Fetches all fixtures in the given date window (inclusive).
        /// Used by FixtureSyncJob to auto-create / update matches.
        /// Returns an empty list on provider failure — never throws.
        /// </summary>
        Task<List<SportsFixtureResult>> GetFixturesAsync(
            DateTime fromDate,
            DateTime toDate,
            CancellationToken ct = default);

        /// <summary>
        /// TOPLU GÜN ÇEKİMİ — verilen günlerin her biri için TEK <c>fixtures?date=</c> isteği.
        /// Maç başına istek ÜRETİLMEZ; bir günün yanıtı o güne düşen bütün maçları günceller.
        ///
        /// <see cref="GetFixturesAsync"/>'ten farkı: günler bitişik bir pencere olmak zorunda
        /// değildir (sonuç uzlaştırma geçmişteki dağınık günleri ister) ve TEK bir günün
        /// başarısızlığı diğer günlerin verisini ÇÖPE ATMAZ — başarı/başarısızlık ayrı ayrı
        /// döner. Erişilemeyen gün "o gün maç yok" DEĞİLDİR.
        /// </summary>
        Task<SportsFixtureDayBatch> GetFixturesForDatesAsync(
            IReadOnlyList<DateTime> dates,
            CancellationToken ct = default);

        /// <summary>
        /// TEK FİKSTÜRÜN KESİN SONUCU — <c>GET /fixtures?id={id}</c>.
        ///
        /// SON ÇARE yoludur, varsayılan yol DEĞİLDİR. Yalnız gün-bazlı toplu çekimin
        /// ABONELİK PLANI tarafından kapatıldığı geçmiş günler için kullanılır (ölçüldü
        /// 31.08.2026: Free plan <c>date=</c> için yalnız [bugün-1, bugün+1] veriyor,
        /// <c>ids=</c> toplu parametresini hiç vermiyor, ama <c>id=</c> tekil sorguyu
        /// kabul ediyor). Erişilebilir günlerde ASLA çağrılmaz — orada tek gün isteği
        /// zaten o günün bütün maçlarını kapatır.
        ///
        /// Sağlayıcı gövde hatası verirse null döner; çağıran sahte sonuç YAZMAZ.
        /// </summary>
        Task<SportsFixtureResult?> GetFixtureByIdAsync(
            string externalMatchId,
            CancellationToken ct = default);

        /// <summary>
        /// Fixture Expansion v2 — a team's most recent FINISHED matches, by external team id,
        /// via api-football GET /fixtures?team={id}&last={N} (all competitions: league / cup /
        /// europe / national / friendly — as the provider supplies them). Used by
        /// HistoricalSyncJob to build the per-team Timeline (past leg). Returns an empty list
        /// on provider failure or when the team has no coverage — never throws.
        /// Reuses the SportsFixtureResult shape (a past result is a scored fixture).
        /// </summary>
        Task<List<SportsFixtureResult>> GetTeamRecentResultsAsync(
            string externalTeamId,
            CancellationToken ct = default);

        /// <summary>
        /// Fixture Expansion v2 — a team's next UPCOMING fixtures, by external team id, via
        /// api-football GET /fixtures?team={id}&next={N} (all competitions, exactly as the
        /// provider supplies them — no synthetic entries). Used by HistoricalSyncJob to build
        /// the per-team Timeline (future leg). Returns an empty list on provider failure or
        /// when the team has no scheduled fixtures — never throws.
        /// </summary>
        Task<List<SportsFixtureResult>> GetTeamUpcomingFixturesAsync(
            string externalTeamId,
            CancellationToken ct = default);

        // ── Sprint 1: Lineup ──────────────────────────────────────────────────

        /// <summary>
        /// Fetches the official starting XI and bench for a fixture.
        /// Returns null when the provider has no lineup data yet (lineup not released).
        /// </summary>
        Task<SportsLineupResult?> GetOfficialLineupAsync(
            string matchExternalId,
            CancellationToken ct = default);

        /// <summary>
        /// Fetches injury / suspension / doubtful statuses for a fixture.
        /// Returns an empty list when no data is available.
        /// </summary>
        Task<List<SportsPlayerStatusResult>> GetPlayerStatusesAsync(
            string matchExternalId,
            CancellationToken ct = default);

        // ── Sprint 2: Standings & Competition Context ─────────────────────────

        /// <summary>
        /// Fetches the current league standings table for a given external league + season.
        /// Returns an empty list when the provider has no data.
        /// </summary>
        Task<List<SportsStandingEntry>> GetLeagueStandingsAsync(
            string leagueExternalId,
            int season,
            CancellationToken ct = default);

        /// <summary>
        /// Fetches competition context (type, stage, round) for a specific fixture.
        /// Returns null when the provider has no data.
        /// </summary>
        Task<SportsCompetitionContext?> GetCompetitionContextAsync(
            string matchExternalId,
            CancellationToken ct = default);

        /// <summary>
        /// Phase 6 — a team's SEASON aggregate statistics (api-football /teams/statistics):
        /// played/win/draw/lose, goals for/against averages (total/home/away), clean-sheets,
        /// failed-to-score, form. Coverage-gated: returns null when the provider has no data
        /// for this team+league+season (no fake).
        /// </summary>
        Task<SportsTeamStatistics?> GetTeamSeasonStatisticsAsync(
            string leagueExternalId,
            string teamExternalId,
            int season,
            CancellationToken ct = default);

        /// <summary>
        /// Phase 6 / Slice 2 — the provider's own match prediction (api-football /predictions?fixture=):
        /// win percentages, predicted winner, advice, comparison strengths. AI-ONLY signal —
        /// never surfaced to the user. Coverage-gated: returns null when the provider has no
        /// prediction for this fixture (no fake).
        /// </summary>
        Task<SportsMatchPrediction?> GetMatchPredictionAsync(
            string matchExternalId,
            CancellationToken ct = default);

        /// <summary>
        /// Phase 6 Final — a team's profile bundle from api-football: coach (/coachs),
        /// venue (/teams), squad (/players/squads), recent transfers (/transfers). AI/GDP-only.
        /// Each sub-block is independently coverage-gated (Has* flags). Returns null only when
        /// the team id is invalid or the provider is unreachable (no fake).
        /// </summary>
        Task<SportsTeamProfile?> GetTeamProfileAsync(
            string teamExternalId,
            CancellationToken ct = default);

        // ── Sprint 3: Live match intelligence ─────────────────────────────────

        /// <summary>
        /// Single-call batch endpoint: returns a lightweight summary for EVERY
        /// currently-live fixture (score + clock only).  Use this once per cycle
        /// to determine which matches have changed before issuing per-match
        /// detailed stat requests.
        /// </summary>
        Task<List<SportsLiveBatchEntry>> GetAllLiveFixturesAsync(
            CancellationToken ct = default);

        /// <summary>
        /// Fetches the current live statistics snapshot for a fixture.
        /// When <paramref name="batchEntry"/> is supplied (score + clock already fetched
        /// by the batch endpoint), the redundant GET /fixtures?id= call is skipped and
        /// only GET /fixtures/statistics?fixture= is issued (1 request instead of 2).
        /// Pass null only from callers that do not have a batch entry (e.g. GetLiveMomentumAsync).
        /// </summary>
        Task<SportsLiveStats?> GetLiveMatchStatsAsync(
            string matchExternalId,
            SportsLiveBatchEntry? batchEntry,
            CancellationToken ct = default);

        /// <summary>
        /// Fetches the event timeline (goals, cards, subs, VAR) for a fixture.
        /// Returns an empty list when no events are available.
        /// </summary>
        Task<List<SportsLiveEvent>> GetLiveMatchEventsAsync(
            string matchExternalId,
            CancellationToken ct = default);

        /// <summary>
        /// Derives a momentum snapshot for a fixture from available live stats.
        /// Returns null when no stats are available to derive from.
        /// </summary>
        Task<SportsLiveMomentum?> GetLiveMomentumAsync(
            string matchExternalId,
            CancellationToken ct = default);

        /// <summary>
        /// Timeline Operations — api-football GET /status: GERÇEK günlük istek kullanımı + plan limiti.
        /// Quota Intelligence dashboard'u için (varsayım yerine ölçülmüş limit). Cached (1 h). Returns
        /// null when the provider is unreachable or the key is missing.
        /// </summary>
        Task<SportsApiStatus?> GetApiStatusAsync(CancellationToken ct = default);

        /// <summary>
        /// Football Intelligence v1.0 — a team's per-player SEASON stats via api-football
        /// GET /players?team={id}&season={season} (paginated). Each player's multiple stat lines
        /// (competitions) are summed. Coverage-gated: empty list when the league/team has no player
        /// coverage (no fake). Used by GDP player-intelligence ingestion.
        /// </summary>
        Task<List<SportsPlayerSeasonStat>> GetTeamPlayersAsync(
            string teamExternalId, int season, CancellationToken ct = default);

        /// <summary>
        /// Football Intelligence v1.0 — a team's NAMED injuries/suspensions via api-football
        /// GET /injuries?team={id}&season={season}. Deduplicated per player (latest). Empty when
        /// no coverage (no fake).
        /// </summary>
        Task<List<SportsTeamInjury>> GetTeamInjuriesAsync(
            string teamExternalId, int season, CancellationToken ct = default);

        // ── Real Market Odds ──────────────────────────────────────────────────

        /// <summary>
        /// GERÇEK market oranları — api-football GET /odds?date=YYYY-MM-DD&amp;page=N.
        /// Gün bazlı sayfalı çekim bilinçli tercihtir: maç-başına /odds?fixture= çağrısı bugünün
        /// ~700 maçı için ~700 istek demektir; gün+sayfa formu aynı kapsamı ~40 istekte verir.
        /// Sağlayıcının bet/value adları burada normalize market anahtarlarına çevrilir; karşılığı
        /// olmayan marketler ATLANIR (uydurma yok). Sağlayıcı hatasında boş sayfa döner, atmaz.
        /// </summary>
        Task<SportsOddsPage> GetOddsByDateAsync(
            DateTime date, int page, CancellationToken ct = default);
    }
}
