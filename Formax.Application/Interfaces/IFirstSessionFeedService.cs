using Formax.Application.DTOs.Recommendations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.Interfaces;

public interface IFirstSessionFeedService
{
    Task<List<RecommendationCardDto>> GetHookMatches(int userId);
}
