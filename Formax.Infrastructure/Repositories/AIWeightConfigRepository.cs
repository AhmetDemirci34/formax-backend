using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Repositories;

public class AIWeightConfigRepository : IAIWeightConfigRepository
{
    private readonly FormaxDbContext _db;

    public AIWeightConfigRepository(FormaxDbContext db)
    {
        _db = db;
    }

    public async Task<AIWeightConfig?> GetActiveAsync()
    {
        return await _db.AIWeightConfigs
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();
    }
}