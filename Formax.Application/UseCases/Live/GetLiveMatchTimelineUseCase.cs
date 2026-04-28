using Formax.Application.DTOs.Live;
using Formax.Application.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Formax.Application.UseCases.Live
{
    public class GetLiveMatchTimelineUseCase
    {
        private readonly IMatchEventReadRepository _matchEventReadRepository;

        public GetLiveMatchTimelineUseCase(
            IMatchEventReadRepository matchEventReadRepository)
        {
            _matchEventReadRepository = matchEventReadRepository;
        }

        public async Task<List<LiveMatchEventDto>> ExecuteAsync(int matchId)
        {
            var events = await _matchEventReadRepository.GetByMatchAsync(matchId);

            return events
                .OrderBy(e => e.Minute)
                .ToList();
        }
    }
}
