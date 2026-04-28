using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;



namespace Formax.Infrastructure.Repositories
{
    public class CouponReadRepository : ICouponReadRepository
    {
        private readonly FormaxDbContext _context;

        public CouponReadRepository(FormaxDbContext context)
        {
            _context = context;
        }

        public bool Exists(int couponId)
        {
            return _context.Coupons.Any(c => c.Id == couponId);
        }

        public Coupon? GetById(int couponId)
        {
            return _context.Coupons.FirstOrDefault(c => c.Id == couponId);
        }

        public List<Coupon> GetByUser(int userId)
        {
            return _context.Coupons
                .Where(c => c.UserId == userId)
                .ToList();
        }

        public async Task<List<Coupon>> GetByUserAndResultAsync( int userId, string result)
        {
            return await _context.Coupons
                .Where(c => c.UserId == userId && c.Result == result)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

    }
}
