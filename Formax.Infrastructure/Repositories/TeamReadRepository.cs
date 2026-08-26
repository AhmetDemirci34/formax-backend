using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data; // ✅ DOĞRU
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Repositories;

public class TeamReadRepository : ITeamReadRepository
{
    private readonly FormaxDbContext _context;

    // İSTEK-İÇİ MEMOIZASYON (yalnız perf; SONUÇ DEĞİŞMEZ).
    // Repo Scoped'tır → sözlük istek başına sıfırlanır, istekler arası sızma yoktur.
    // NEDEN: Tek Discover isteğinde AYNI Team satırı üç ayrı servis tarafından okunuyordu
    // (MatchAiContextBuilder.ResolveExternalId, GucSkoruCalculator.GetTeamCached,
    // MatchComparisonFactory.ComputeTeamComparison) — 200 farklı takım için 589 sorgu.
    // Her servisin kendi özel cache'i vardı, ortak olan yoktu. Bulunamayan takım (null) da
    // saklanır; aksi halde kapsamı olmayan takımlarda sorgu tekrarı sürerdi.
    private readonly Dictionary<int, Team?> _byIdCache = new();

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
        if (_byIdCache.TryGetValue(id, out var cached))
            return cached;

        var team = _context.Teams
            .AsNoTracking()
            .FirstOrDefault(t => t.Id == id);

        _byIdCache[id] = team;
        return team;
    }

    public Team? GetByName(string name)
    {
        return _context.Teams
            .AsNoTracking()
            .FirstOrDefault(t => t.Name == name);
    }
}