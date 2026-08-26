using Formax.Application.DTOs.Leagues;
using Formax.Application.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Formax.Application.UseCases.Leagues
{
    /// <summary>
    /// Kullanıcının takip ettiği ligleri döner. Lig kimliği Match.LeagueId ile aynıdır;
    /// lig adı mevcut maç verisinden çözülür (uydurma yok). Lig logosu kaynağı yoktur → null.
    /// </summary>
    public class GetMyLeaguesUseCase
    {
        private readonly IUserLeagueFollowRepository _follows;
        private readonly IMatchReadRepository _matches;

        public GetMyLeaguesUseCase(
            IUserLeagueFollowRepository follows,
            IMatchReadRepository matches)
        {
            _follows = follows;
            _matches = matches;
        }

        public async Task<List<LeagueDto>> ExecuteAsync(int userId)
        {
            var active = await _follows.GetActiveByUserAsync(userId);
            var ids = active.Select(x => x.LeagueId).Distinct().ToList();
            if (ids.Count == 0)
                return new List<LeagueDto>();

            // Lig adını mevcut maç verisinden çöz (LeagueId → League adı).
            var nameById = _matches.Query()
                .Where(m => ids.Contains(m.LeagueId))
                .GroupBy(m => m.LeagueId)
                .Select(g => new { LeagueId = g.Key, Name = g.Select(x => x.League).FirstOrDefault() })
                .ToDictionary(x => x.LeagueId, x => x.Name);

            return ids
                .Select(id => new LeagueDto
                {
                    Id = id,
                    Name = nameById.TryGetValue(id, out var n) ? (n ?? string.Empty) : string.Empty,
                    LogoUrl = null
                })
                .OrderBy(l => l.Name)
                .ToList();
        }
    }
}
