using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Domain.Entities;

public class UserTasteProfile
{
    public int Id { get; set; }
    public int UserId { get; set; }

    public double RiskLevel { get; set; } = 0.5;
    public double TrendAffinity { get; set; } = 0.5;
    public double ValueSeeking { get; set; } = 0.5;

    public int TotalSwipes { get; set; } = 0;
}