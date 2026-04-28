using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.DTOs.Coupons
{
    public class CouponItemDetailDto
    {
        public int MatchId { get; set; }
        public string PredictionCode { get; set; } = null!;
        public bool? IsSuccess { get; set; }
    }
}
