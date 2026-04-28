using System.Threading.Tasks;
using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    public interface IUserInterestEventRepository
    {
        Task AddAsync(UserInterestEvent entity);
    }
}
