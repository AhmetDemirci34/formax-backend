using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;

namespace Formax.Infrastructure.Repositories
{
    public sealed class UserInterestEventRepository : IUserInterestEventRepository
    {
        private readonly FormaxDbContext _context;

        public UserInterestEventRepository(FormaxDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(UserInterestEvent entity)
        {
            _context.Set<UserInterestEvent>().Add(entity);
            await _context.SaveChangesAsync();
        }
    }
}
