using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Application.Services.Radar.Learning;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Historical.Prediction.Ranking.Personalization;

/// <summary>Bir kullanıcı+maç için <see cref="UserInterestSignal"/> üreten kaynak (kişiselleştirmenin veri seams'i).</summary>
public interface IUserInterestSignalProvider
{
    Task<UserInterestSignal> GetAsync(int userId, int matchId, CancellationToken cancellationToken = default);
}

/// <summary>
/// User Interest sinyalini MEVCUT engine'lerden kurar (yeniden yazmaz): profil <see cref="IUserInterestProfileService"/>
/// (R.14.2), kullanıcı×maç ilgisi <see cref="IMatchAffinityEngine"/> (R.14.3). Historical maç için takım/lig
/// ADLARINI Historical tablolarından çözer → isim-tabanlı profil köprüsüz eşleşir. Decay girdisi (en taze ilgi
/// olayının yaşı) mevcut <see cref="ILearningEventRepository"/>'den; decay MATEMATİĞİ kişiselleştirme motorunda.
/// ImportanceScore=0 verilir (objektif önem zaten Base Radar Score'da; çift sayım yok).
/// </summary>
public sealed class AffinityUserInterestSignalProvider : IUserInterestSignalProvider
{
    private readonly IUserInterestProfileService _profileService;
    private readonly IMatchAffinityEngine _affinityEngine;
    private readonly ILearningEventRepository _events;
    private readonly FormaxDbContext _db;

    public AffinityUserInterestSignalProvider(
        IUserInterestProfileService profileService,
        IMatchAffinityEngine affinityEngine,
        ILearningEventRepository events,
        FormaxDbContext db)
    {
        _profileService = profileService;
        _affinityEngine = affinityEngine;
        _events = events;
        _db = db;
    }

    public async Task<UserInterestSignal> GetAsync(int userId, int matchId, CancellationToken cancellationToken = default)
    {
        var profile = await _profileService.GetProfileAsync(userId, cancellationToken).ConfigureAwait(false);
        var hasProfile = profile.Teams.Count > 0 || profile.Leagues.Count > 0;
        if (!hasProfile) return UserInterestSignal.None(userId, matchId);

        var descriptor = await _db.HistoricalMatches.AsNoTracking()
            .Where(x => x.Id == matchId)
            .Select(x => new { x.HomeTeamId, x.AwayTeamId, x.HistoricalCompetitionId })
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (descriptor is null) return UserInterestSignal.None(userId, matchId);

        var homeName = await NameOfTeam(descriptor.HomeTeamId, cancellationToken).ConfigureAwait(false);
        var awayName = await NameOfTeam(descriptor.AwayTeamId, cancellationToken).ConfigureAwait(false);
        var league = await _db.HistoricalCompetitions.AsNoTracking()
            .Where(c => c.Id == descriptor.HistoricalCompetitionId).Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false) ?? string.Empty;

        // MEVCUT affinity engine (objektif önem 0 → yalnız kullanıcı ilgisi; Base'de zaten var).
        var affinity = _affinityEngine.Compute(profile, new MatchAffinityContext
        {
            MatchId = matchId,
            HomeTeamName = homeName,
            AwayTeamName = awayName,
            League = league,
            ImportanceScore = 0
        });

        // Interest Decay girdisi: en taze ilgi olayının yaşı (gün).
        var recent = await _events.GetByUserAsync(userId, 100, cancellationToken).ConfigureAwait(false);
        var ageDays = recent.Count > 0 ? (DateTime.UtcNow - recent.Max(e => e.OccurredAtUtc)).TotalDays : 0;

        return new UserInterestSignal
        {
            UserId = userId,
            MatchId = matchId,
            AffinityScore = affinity.AffinityScore,
            TeamComponent = affinity.TeamComponent,
            LeagueComponent = affinity.LeagueComponent,
            SignalComponent = affinity.SignalComponent,
            InterestAgeDays = Math.Max(0, ageDays),
            HasData = affinity.AffinityScore > 0
        };
    }

    private async Task<string> NameOfTeam(int teamId, CancellationToken ct)
        => await _db.HistoricalTeams.AsNoTracking().Where(t => t.Id == teamId).Select(t => t.Name)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false) ?? string.Empty;
}
