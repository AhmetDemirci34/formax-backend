using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Repositories
{
    public class TeamRepository : ITeamRepository
    {
        private readonly FormaxDbContext _context;

        public TeamRepository(FormaxDbContext context)
        {
            _context = context;
        }

        public Team GetById(int id)
        {
            var team = _context.Teams.Find(id);

            return team ?? throw new Exception($"Team bulunamadı. Id: {id}");
        }

        public Team GetByName(string name)
        {
            var team = _context.Teams
                .FirstOrDefault(x => x.Name == name);

            return team ?? throw new Exception($"Team bulunamadı. Name: {name}");
        }

        public async Task<List<Team>> GetAllAsync()
        {
            return await _context.Teams.ToListAsync();
        }

        public async Task<List<Team>> GetByIdsAsync(List<int> teamIds)
        {
            return await _context.Teams
                .Where(t => teamIds.Contains(t.Id))
                .ToListAsync();
        }

        public List<Team> GetAll()
        {
            return _context.Teams.ToList();
        }
    }
}