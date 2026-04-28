using Formax.Application.Interfaces;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Repositories
{
    public class CouponResultRepository : ICouponResultRepository
    {
        private readonly FormaxDbContext _context;

        public CouponResultRepository(FormaxDbContext context)
        {
            _context = context;
        }

        public void UpdateResult(int couponId, string result)
        {
            var coupon = _context.Coupons
                .FirstOrDefault(c => c.Id == couponId);

            if (coupon == null)
                return;

            coupon.Result = result;
            _context.SaveChanges();
        }
    }
}
