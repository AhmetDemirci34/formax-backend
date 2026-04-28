using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Formax.Application.Interfaces
{
    public interface IHomeInterestProfileService
    {
        Task<HomeInterestProfileDto> BuildAsync(int? userId, DateTime utcNow);
    }

    public sealed class HomeInterestProfileDto
    {
        public IReadOnlyDictionary<string, int> TeamScores { get; init; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public IReadOnlyDictionary<string, int> LeagueScores { get; init; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public IReadOnlyDictionary<string, int> ContentScores { get; init; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    }
}
