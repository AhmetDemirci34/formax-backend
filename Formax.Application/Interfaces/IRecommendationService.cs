using Formax.Application.DTOs.Home;
using Formax.Application.DTOs.Recommendations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.Interfaces;

public interface IRecommendationService
{
    Task<List<RecommendationCardDto>> GetFeedAsync(int userId);
    Task<List<RecommendationCardDto>> GetFeedAsync(int userId, List<HomeRadarMatchDto> matches);
}
