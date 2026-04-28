using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data; // ✅ DOĞRU
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Repositories;

public class TeamReadRepository : ITeamReadRepository
{
    private readonly FormaxDbContext _context;

    public TeamReadRepository(FormaxDbContext context)
    {
        _context = context;
    }

    public IQueryable<Team> Query()
    {
        return _context.Teams.AsNoTracking();
    }

    public Team? GetById(int id)
    {
        return _context.Teams
            .AsNoTracking()
            .FirstOrDefault(t => t.Id == id);
    }

    public Team? GetByName(string name)
    {
        return _context.Teams
            .AsNoTracking()
            .FirstOrDefault(t => t.Name == name);
    }
}