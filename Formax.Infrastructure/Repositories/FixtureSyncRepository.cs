using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;

namespace Formax.Infrastructure.Repositories;

/// <summary>
/// Batch-oriented repository for FixtureSyncJob.
/// Two queries per sync cycle regardless of fixture count.
/// </summary>
public class FixtureSyncRepository : IFixtureSyncRepository
{
    private readonly FormaxDbContext _context;

    public FixtureSyncRepository(FormaxDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public Dictionary<string, Team> GetTeamsByExternalIds(IEnumerable<string> externalIds)
    {
        var ids = externalIds.ToHashSet();
        if (ids.Count == 0) return new Dictionary<string, Team>();

        // DistinctBy guards against duplicate ExternalTeamId rows that could
        // exist in a dirty DB (race window before unique index was enforced).
        // AsEnumerable() materialises the WHERE result in memory first so that
        // DistinctBy runs client-side — safe because the set is bounded by ids.Count.
        return _context.Teams
            .Where(t => t.ExternalTeamId != null && ids.Contains(t.ExternalTeamId))
            .AsEnumerable()
            .DistinctBy(t => t.ExternalTeamId!)
            .ToDictionary(t => t.ExternalTeamId!, t => t);
    }

    /// <inheritdoc />
    public Dictionary<string, Match> GetMatchesByExternalIds(IEnumerable<string> externalIds)
    {
        var ids = externalIds.ToHashSet();
        if (ids.Count == 0) return new Dictionary<string, Match>();

        // Same duplicate-safe pattern as GetTeamsByExternalIds.
        return _context.Matches
            .Where(m => m.ExternalMatchId != null && ids.Contains(m.ExternalMatchId))
            .AsEnumerable()
            .DistinctBy(m => m.ExternalMatchId!)
            .ToDictionary(m => m.ExternalMatchId!, m => m);
    }

    /// <inheritdoc />
    public void AddTeam(Team team)
        => _context.Teams.Add(team);

    /// <inheritdoc />
    public void AddMatch(Match match)
        => _context.Matches.Add(match);

    /// <inheritdoc />
    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
