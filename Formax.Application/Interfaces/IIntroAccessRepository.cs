using System.Threading.Tasks;
using Formax.Domain.Subscriptions;

namespace Formax.Application.Interfaces;

public interface IIntroAccessRepository
{
    Task<IntroAccess?> GetByUserIdAsync(Guid userId);
}
