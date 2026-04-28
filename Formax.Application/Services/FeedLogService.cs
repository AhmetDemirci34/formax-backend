using System.Threading.Tasks;
using Formax.Domain.Entities;
using Formax.Application.Interfaces.Repositories;

namespace Formax.Application.Services.Recommendation;

public class FeedLogService
{
    private readonly IFeedScoreLogRepository _repo;

    public FeedLogService(IFeedScoreLogRepository repo)
    {
        _repo = repo;
    }

    public async Task LogAsync(FeedScoreLog log)
    {
        try
        {
            await _repo.SaveAsync(log);
        }
        catch
        {
            // fail-safe
        }
    }
}