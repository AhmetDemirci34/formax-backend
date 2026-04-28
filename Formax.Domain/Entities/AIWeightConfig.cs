using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Domain.Entities;

public class AIWeightConfig
{
    public int Id { get; set; }

    public double InterestWeight { get; set; }
    public double BanditWeight { get; set; }
    public double SessionWeight { get; set; }
    public double TrendWeight { get; set; }
    public double DiversityWeight { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }
}
