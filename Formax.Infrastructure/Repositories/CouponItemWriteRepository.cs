using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;

namespace Formax.Infrastructure.Repositories
{
    public class CouponItemWriteRepository : ICouponItemWriteRepository
    {
        private readonly FormaxDbContext _context;

        public CouponItemWriteRepository(FormaxDbContext context)
        {
            _context = context;
        }

        public void Add(CouponItem item)
        {
            _context.CouponItems.Add(item);
            _context.SaveChanges();
        }

        public void Update(CouponItem item)
        {
            _context.CouponItems.Update(item);
            _context.SaveChanges();
        }
    }
}
