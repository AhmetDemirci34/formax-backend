using Formax.Application.Interfaces;
using System;
using System.Threading.Tasks;

namespace Formax.Application.UseCases.Live
{
    public class EmitMatchEventUseCase
    {
        private readonly IMatchEventWriteRepository _matchEventWriteRepository;
        private readonly IInterestEngine _interestEngine;

        public EmitMatchEventUseCase(
            IMatchEventWriteRepository matchEventWriteRepository,
            IInterestEngine interestEngine)
        {
            _matchEventWriteRepository = matchEventWriteRepository;
            _interestEngine = interestEngine;
        }

            public async Task ExecuteAsync(
            int matchId,
            string eventType,
            int minute,
            string teamName,
            string? playerName,
            string description,
            int? userId)
        {
            await _matchEventWriteRepository.AddAsync(
                matchId,
                eventType,
                minute,
                teamName,
                playerName,
                description
            );

            // logged-in yoksa interest yazma
            if (userId.HasValue)
            {
                await _interestEngine.ProcessMatchEventAsync(
                    userId.Value,
                    matchId,
                    eventType,
                    teamName,
                    DateTime.UtcNow
                );
            }
        }
    }
    
}