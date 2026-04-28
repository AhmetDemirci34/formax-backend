using System.Threading.Tasks;
using Formax.Domain.Entities;
using Formax.Application.Interfaces.Repositories;
using Formax.Infrastructure.Data;

public class FeedScoreLogRepository : IFeedScoreLogRepository
{
    private readonly FormaxDbContext _db;

    public FeedScoreLogRepository(FormaxDbContext db)
    {
        _db = db;
    }

    public async Task SaveAsync(FeedScoreLog log)
    {
        _db.FeedScoreLogs.Add(log);
        await _db.SaveChangesAsync();
    }
}