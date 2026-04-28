using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.UseCases.Coupons
{
    public class AddCouponItemRequest
    {
        public int CouponId { get; set; }
        public int MatchId { get; set; }
        public int PredictionTypeId { get; set; }
    }
}
