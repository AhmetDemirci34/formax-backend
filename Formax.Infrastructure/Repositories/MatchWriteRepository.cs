using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;

namespace Formax.Infrastructure.Repositories
{
    public class MatchWriteRepository : IMatchWriteRepository
    {
        private readonly FormaxDbContext _context;

        public MatchWriteRepository(FormaxDbContext context)
        {
            _context = context;
        }

        public void Update(Match match)
        {
            _context.Matches.Update(match);
            _context.SaveChanges();
        }
    }
}
