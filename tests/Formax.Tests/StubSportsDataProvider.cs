using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.DTOs.Diagnostics;
using Formax.Application.DTOs.Fixtures;
using Formax.Application.DTOs.Lineup;
using Formax.Application.DTOs.Live;
using Formax.Application.DTOs.Odds;
using Formax.Application.DTOs.Players;
using Formax.Application.DTOs.Predictions;
using Formax.Application.DTOs.Standings;
using Formax.Application.Interfaces;

namespace Formax.Tests;

/// <summary>
/// SAĞLAYICI STUB TABANI — testlerin YALNIZ ilgilendikleri ucu geçersiz kılabilmesi için.
///
/// Geçersiz kılınmayan her uç <see cref="NotSupportedException"/> atar: bir test yanlışlıkla
/// beklemediği bir sağlayıcı ucunu çağırırsa sessizce boş veri almak yerine DÜŞER.
/// Hiçbir üye gerçek ağ trafiği üretmez.
/// </summary>
internal abstract class StubSportsDataProvider : ISportsDataProvider
{
    private static Task<T> No<T>([System.Runtime.CompilerServices.CallerMemberName] string member = "")
        => throw new NotSupportedException($"Test bu ucu beklemiyordu: {member}");

    public virtual Task<List<SportsFixtureResult>> GetFixturesAsync(
        DateTime fromDate, DateTime toDate, CancellationToken ct = default) => No<List<SportsFixtureResult>>();

    public virtual Task<SportsFixtureDayBatch> GetFixturesForDatesAsync(
        IReadOnlyList<DateTime> dates, CancellationToken ct = default) => No<SportsFixtureDayBatch>();

    public virtual Task<SportsFixtureResult?> GetFixtureByIdAsync(
        string externalMatchId, CancellationToken ct = default) => No<SportsFixtureResult?>();

    public virtual Task<List<SportsFixtureResult>> GetTeamRecentResultsAsync(
        string externalTeamId, CancellationToken ct = default) => No<List<SportsFixtureResult>>();

    public virtual Task<List<SportsFixtureResult>> GetTeamUpcomingFixturesAsync(
        string externalTeamId, CancellationToken ct = default) => No<List<SportsFixtureResult>>();

    public virtual Task<SportsLineupResult?> GetOfficialLineupAsync(
        string matchExternalId, CancellationToken ct = default) => No<SportsLineupResult?>();

    public virtual Task<List<SportsPlayerStatusResult>> GetPlayerStatusesAsync(
        string matchExternalId, CancellationToken ct = default) => No<List<SportsPlayerStatusResult>>();

    public virtual Task<List<SportsStandingEntry>> GetLeagueStandingsAsync(
        string leagueExternalId, int season, CancellationToken ct = default) => No<List<SportsStandingEntry>>();

    public virtual Task<SportsCompetitionContext?> GetCompetitionContextAsync(
        string matchExternalId, CancellationToken ct = default) => No<SportsCompetitionContext?>();

    public virtual Task<SportsTeamStatistics?> GetTeamSeasonStatisticsAsync(
        string leagueExternalId, string teamExternalId, int season, CancellationToken ct = default)
        => No<SportsTeamStatistics?>();

    public virtual Task<SportsMatchPrediction?> GetMatchPredictionAsync(
        string matchExternalId, CancellationToken ct = default) => No<SportsMatchPrediction?>();

    public virtual Task<SportsTeamProfile?> GetTeamProfileAsync(
        string teamExternalId, CancellationToken ct = default) => No<SportsTeamProfile?>();

    public virtual Task<List<SportsLiveBatchEntry>> GetAllLiveFixturesAsync(
        CancellationToken ct = default) => No<List<SportsLiveBatchEntry>>();

    public virtual Task<SportsLiveStats?> GetLiveMatchStatsAsync(
        string matchExternalId, SportsLiveBatchEntry? batchEntry, CancellationToken ct = default)
        => No<SportsLiveStats?>();

    public virtual Task<List<SportsLiveEvent>> GetLiveMatchEventsAsync(
        string matchExternalId, CancellationToken ct = default) => No<List<SportsLiveEvent>>();

    public virtual Task<SportsLiveMomentum?> GetLiveMomentumAsync(
        string matchExternalId, CancellationToken ct = default) => No<SportsLiveMomentum?>();

    public virtual Task<SportsApiStatus?> GetApiStatusAsync(CancellationToken ct = default)
        => No<SportsApiStatus?>();

    public virtual Task<List<SportsPlayerSeasonStat>> GetTeamPlayersAsync(
        string teamExternalId, int season, CancellationToken ct = default) => No<List<SportsPlayerSeasonStat>>();

    public virtual Task<List<SportsTeamInjury>> GetTeamInjuriesAsync(
        string teamExternalId, int season, CancellationToken ct = default) => No<List<SportsTeamInjury>>();

    public virtual Task<SportsOddsPage> GetOddsByDateAsync(
        DateTime date, int page, CancellationToken ct = default) => No<SportsOddsPage>();
}
