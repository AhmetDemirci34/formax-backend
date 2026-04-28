using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Formax.Application.DTOs.Coupons
{
    public class CouponDetailDto
    {
        public int CouponId { get; set; }
        public string Result { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public List<CouponItemDetailDto> Items { get; set; } = new();
        public string? AiSummary { get; set; }
    }
}
