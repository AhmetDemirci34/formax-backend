using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Formax.Application.DTOs.Matches;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Repositories
{
    public class MatchReadRepository : IMatchReadRepository
    {
        private readonly FormaxDbContext _context;

        public MatchReadRepository(FormaxDbContext context)
        {
            _context = context;
        }

        // 🔥 GLOBAL QUERY (FULL INCLUDE)
        public IQueryable<Match> Query()
        {
            return _context.Matches
                .Include(x => x.HomeTeam)
                .Include(x => x.AwayTeam)
                .AsNoTracking();
        }

        // 🔥 FIXED — INCLUDE EKLENDİ
        public Match? GetById(int id)
        {
            return _context.Matches
                .Include(x => x.HomeTeam)
                .Include(x => x.AwayTeam)
                .AsNoTracking()
                .FirstOrDefault(m => m.Id == id);
        }

        // 🔥 FIXED — INCLUDE EKLENDİ
        public List<Match> GetUpcomingMatches(DateTime from, DateTime to)
        {
            return _context.Matches
                .Include(x => x.HomeTeam)
                .Include(x => x.AwayTeam)
                .AsNoTracking()
                .Where(m =>
                    m.MatchDate >= from &&
                    m.MatchDate <= to)
                .OrderBy(m => m.MatchDate)
                .ToList();
        }

        // 🔥 LIST API (DTO projection — DOĞRU)
        public async Task<IReadOnlyList<MatchListItemDto>> GetMatchListAsync()
        {
            var startDate = DateTime.UtcNow.AddDays(-30);
            var endDate = DateTime.UtcNow.AddDays(30);

            var query =
                from m in _context.Matches.AsNoTracking()

                where m.MatchDate >= startDate
                      && m.MatchDate <= endDate

                join ht in _context.Teams.AsNoTracking()
                    on m.HomeTeamId equals ht.Id into htj
                from home in htj.DefaultIfEmpty()

                join at in _context.Teams.AsNoTracking()
                    on m.AwayTeamId equals at.Id into atj
                from away in atj.DefaultIfEmpty()

                orderby m.MatchDate descending

                select new MatchListItemDto
                {
                    MatchId = m.Id,
                    HomeTeam = home != null ? home.Name : "Team A",
                    AwayTeam = away != null ? away.Name : "Team B",
                    League = string.IsNullOrWhiteSpace(m.League)
                        ? "superlig"
                        : m.League.ToLower(),
                    StartTime = m.MatchDate,
                    Score = null,
                    Minute = null,
                    Status = m.Status
                };

            var list = await query.Take(50).ToListAsync();

            return list;
        }
    }
}