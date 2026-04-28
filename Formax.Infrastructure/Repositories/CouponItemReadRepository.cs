using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Repositories
{
    public class CouponItemReadRepository : ICouponItemReadRepository
    {
        private readonly FormaxDbContext _context;

        public CouponItemReadRepository(FormaxDbContext context)
        {
            _context = context;
        }

        public List<CouponItem> GetByCouponId(int couponId)
        {
            return _context.CouponItems
                .Include(x => x.PredictionType)
                .Where(x => x.CouponId == couponId)
                .ToList();
        }

        public bool ExistsByGroup(int couponId, int matchId, string groupCode)
        {
            return _context.CouponItems
                .Include(x => x.PredictionType)
                .Any(x =>
                    x.CouponId == couponId &&
                    x.MatchId == matchId &&
                    x.PredictionType!.GroupCode == groupCode
                );
        }
    }
}
