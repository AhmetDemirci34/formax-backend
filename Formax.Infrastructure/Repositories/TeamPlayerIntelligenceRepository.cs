using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Repositories
{
    /// <summary>
    /// Football Intelligence v1.0 — TeamPlayerIntelligence upsert/okuma. TeamProfileSignalRepository ile
    /// aynı desen (PK internal TeamId).
    /// </summary>
    public sealed class TeamPlayerIntelligenceRepository : ITeamPlayerIntelligenceRepository
    {
        private readonly FormaxDbContext _db;

        public TeamPlayerIntelligenceRepository(FormaxDbContext db) => _db = db;

        public TeamPlayerIntelligence? GetByTeam(int teamId)
            => _db.TeamPlayerIntelligences.AsNoTracking().FirstOrDefault(x => x.TeamId == teamId);

        public async Task UpsertAsync(TeamPlayerIntelligence entity, CancellationToken ct = default)
        {
            var existing = await _db.TeamPlayerIntelligences.FirstOrDefaultAsync(x => x.TeamId == entity.TeamId, ct);
            if (existing == null)
            {
                _db.TeamPlayerIntelligences.Add(entity);
            }
            else
            {
                existing.ExternalTeamId = entity.ExternalTeamId;
                existing.Season = entity.Season;
                existing.HasData = entity.HasData;
                existing.SquadPlayerCount = entity.SquadPlayerCount;
                existing.GkCount = entity.GkCount;
                existing.DefCount = entity.DefCount;
                existing.MidCount = entity.MidCount;
                existing.AttCount = entity.AttCount;
                existing.TopScorerName = entity.TopScorerName;
                existing.TopScorerGoals = entity.TopScorerGoals;
                existing.TopScorerAssists = entity.TopScorerAssists;
                existing.TopScorerRating = entity.TopScorerRating;
                existing.TopAssistName = entity.TopAssistName;
                existing.TopAssistCount = entity.TopAssistCount;
                existing.KeyPlayerName = entity.KeyPlayerName;
                existing.KeyPlayerRating = entity.KeyPlayerRating;
                existing.MinutesLeaderName = entity.MinutesLeaderName;
                existing.MinutesLeaderMinutes = entity.MinutesLeaderMinutes;
                existing.InjuredCount = entity.InjuredCount;
                existing.InjuredNames = entity.InjuredNames;
                existing.InjuredDefCount = entity.InjuredDefCount;
                existing.InjuredMidCount = entity.InjuredMidCount;
                existing.InjuredAttCount = entity.InjuredAttCount;
                existing.TeamTotalGoals = entity.TeamTotalGoals;
                existing.TopScorerGoalSharePct = entity.TopScorerGoalSharePct;
                existing.Top2GoalSharePct = entity.Top2GoalSharePct;
                existing.OneManDependency = entity.OneManDependency;
                existing.DefenseLeaderName = entity.DefenseLeaderName;
                existing.DefenseLeaderRating = entity.DefenseLeaderRating;
                existing.MidfieldBrainName = entity.MidfieldBrainName;
                existing.MidfieldBrainAssists = entity.MidfieldBrainAssists;
                existing.MidfieldBrainRating = entity.MidfieldBrainRating;
                existing.ShotsLeaderName = entity.ShotsLeaderName;
                existing.ShotsLeaderCount = entity.ShotsLeaderCount;
                existing.KeyPassLeaderName = entity.KeyPassLeaderName;
                existing.KeyPassLeaderCount = entity.KeyPassLeaderCount;
                existing.CardRiskName = entity.CardRiskName;
                existing.CardRiskYellows = entity.CardRiskYellows;
                existing.UpdatedAt = entity.UpdatedAt;
            }
            await _db.SaveChangesAsync(ct);
        }
    }
}
