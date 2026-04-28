using Formax.Application.DTOs.Coupons;
using Formax.Application.Interfaces;
using Formax.Application.UseCases.Coupons;
using Formax.Domain.Entities;

namespace Formax.Application.Services
{
    public class CouponQueryService
    {
        private readonly ICouponReadRepository _readRepository;

        public CouponQueryService(ICouponReadRepository readRepository)
        {
            _readRepository = readRepository;
        }

        // SADECE LİSTE / ÖZET İŞİ YAPAR
        public List<CouponListItemDto> GetByUser(int userId)
        {
            var coupons = _readRepository.GetByUser(userId);

            return coupons.Select(c => new CouponListItemDto
            {
                CouponId = c.Id,
                CreatedAt = c.CreatedAt,
                Result = c.Result ?? string.Empty,
                Status = c.Status
            }).ToList();
        }
    }
}
