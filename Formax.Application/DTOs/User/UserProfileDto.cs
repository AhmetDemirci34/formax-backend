using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.DTOs.User;

public class UserProfileDto
{
    public double RiskLevel { get; set; }
    public double AvgViewTime { get; set; }
    public double LikeRate { get; set; }
    public double SkipRate { get; set; }
    public double EngagementScore { get; set; }
}
