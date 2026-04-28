using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;

namespace Formax.Infrastructure.Repositories
{
    public class CouponWriteRepository : ICouponWriteRepository
    {
        private readonly FormaxDbContext _context;

        public CouponWriteRepository(FormaxDbContext context)
        {
            _context = context;
        }

        public Coupon Add(Coupon coupon)
        {
            _context.Coupons.Add(coupon);
            _context.SaveChanges();
            return coupon;
        }

        public void Update(Coupon coupon)
        {
            _context.Coupons.Update(coupon);
            _context.SaveChanges();
        }
    }
}
