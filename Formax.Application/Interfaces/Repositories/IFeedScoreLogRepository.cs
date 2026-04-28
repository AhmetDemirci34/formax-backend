using System.Threading.Tasks;
using Formax.Domain.Entities;

public interface IFeedScoreLogRepository
{
    Task SaveAsync(FeedScoreLog log);
}
