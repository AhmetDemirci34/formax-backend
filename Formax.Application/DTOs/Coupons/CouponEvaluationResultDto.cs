using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.DTOs.Coupons;

public class CouponEvaluationResultDto
{
    public double AverageConfidenceScore { get; set; }

    public string Comment { get; set; } = string.Empty;
}
