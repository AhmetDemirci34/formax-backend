using Formax.Application.Interfaces;
using Formax.Application.Interfaces.Repositories;
using Formax.Domain.States;

namespace Formax.Application.UseCases.Home
{
    public class ResolveHomeAIStateUseCase
    {
        private readonly IMatchReadRepository _matchReadRepository;

        public ResolveHomeAIStateUseCase(
            IMatchReadRepository matchReadRepository)
        {
            _matchReadRepository = matchReadRepository;
        }

        public AIUxState Execute()
        {
            var todayMatches = _matchReadRepository.Query().ToList();

            if (!todayMatches.Any())
                return AIUxState.Silent;

            if (todayMatches.Any(m => m.Status == "Live"))
                return AIUxState.Extended;

            return AIUxState.Short;
        }
    }
}
