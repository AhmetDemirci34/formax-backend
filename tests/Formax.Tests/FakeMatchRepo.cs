using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Formax.Application.DTOs.Matches;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;

namespace Formax.Tests;

/// <summary>
/// BELLEK İÇİ MAÇ DEPOSU — yalnız <see cref="GetById"/> gerçek cevap verir.
///
/// Geçersiz kılınmayan her üye <see cref="NotSupportedException"/> atar: bir test
/// beklemediği bir okuma yaptığında sessizce boş liste almak yerine DÜŞER.
/// Gerçek veritabanı ve gerçek ağ KULLANILMAZ.
/// </summary>
internal sealed class FakeMatchRepo : IMatchReadRepository
{
    private readonly Dictionary<int, Match> _matches;

    public FakeMatchRepo(params Match[] matches)
        => _matches = matches.ToDictionary(m => m.Id);

    public Match? GetById(int id) => _matches.TryGetValue(id, out var m) ? m : null;

    private static T No<T>([System.Runtime.CompilerServices.CallerMemberName] string member = "")
        => throw new NotSupportedException($"Test bu okumayı beklemiyordu: {member}");

    public IQueryable<Match> Query() => _matches.Values.AsQueryable();

    public List<Match> GetUpcomingMatches(DateTime from, DateTime to)
        => _matches.Values.Where(m => m.MatchDate >= from && m.MatchDate <= to).ToList();

    public Task<IReadOnlyList<MatchListItemDto>> GetMatchListAsync()
        => No<Task<IReadOnlyList<MatchListItemDto>>>();

    public Task<IReadOnlyList<MatchListItemDto>> GetScreenMatchListAsync(int take = 250)
        => No<Task<IReadOnlyList<MatchListItemDto>>>();

    public Task<IReadOnlyList<MatchListItemDto>> GetMatchListByIdsAsync(IEnumerable<int> ids)
        => No<Task<IReadOnlyList<MatchListItemDto>>>();

    public List<Match> GetRecentMatchesForTeam(
        int teamId, int count = 5, bool finishedOnly = false, int? leagueId = null)
        => No<List<Match>>();

    public List<Match> GetHeadToHeadMatches(
        int homeTeamId, int awayTeamId, int count = 5, bool finishedOnly = false, int? excludeMatchId = null)
        => No<List<Match>>();

    public List<Match> GetSeasonLeagueMatchesForTeam(
        int teamId, int leagueId, DateTime seasonStartUtc, DateTime beforeUtc, int max = 20)
        => No<List<Match>>();

    public List<Match> GetSettledLeagueMatchesInSeason(
        int leagueId, DateTime seasonStartUtc, DateTime seasonEndUtc)
        => No<List<Match>>();

    public List<Match> GetSeasonLeagueFixturesBefore(
        int leagueId, DateTime seasonStartUtc, DateTime seasonEndUtc, DateTime kickoffBeforeUtc)
        => No<List<Match>>();
}
