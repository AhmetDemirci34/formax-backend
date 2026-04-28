using Formax.Application.Interfaces;

namespace Formax.Application.Services
{
    public class MatchLiveService
    {
        private readonly IMatchUpdateRepository _repository;

        public MatchLiveService(IMatchUpdateRepository repository)
        {
            _repository = repository;
        }

        public void UpdateLive(int matchId, string status, string? matchMinute)
        {
            _repository.UpdateLiveStatus(matchId, status, matchMinute);
        }
    }
}


