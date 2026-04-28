using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace Formax.Infrastructure.Repositories
{
    public sealed class MatchInterestReadRepository : IMatchInterestReadRepository
    {
        private readonly FormaxDbContext _context;

        public MatchInterestReadRepository(FormaxDbContext context)
        {
            _context = context;
        }

        public async Task<MatchInterestReadModel?> GetByIdAsync(int matchId)
        {
            return await _context.Matches
                .Where(x => x.Id == matchId)
                .Join(
                    _context.Teams,
                    match => match.HomeTeamId,
                    team => team.Id,
                    (match, homeTeam) => new { match, homeTeam })
                .Join(
                    _context.Teams,
                    left => left.match.AwayTeamId,
                    team => team.Id,
                    (left, awayTeam) => new MatchInterestReadModel
                    {
                        MatchId = left.match.Id,
                        HomeTeamId = left.homeTeam.Id,
                        HomeTeamName = left.homeTeam.Name,
                        AwayTeamId = awayTeam.Id,
                        AwayTeamName = awayTeam.Name,
                        LeagueName = left.match.League
                    })
                .FirstOrDefaultAsync();
        }
    }
}