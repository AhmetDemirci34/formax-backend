using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Formax.Application.Services.Radar.Intelligence.Odds
{
    /// <summary>
    /// Radar Odds Movement (R.11.2) — default orchestrator. Flow per reading:
    ///   1. read the latest existing snapshot for the match (the "previous")
    ///   2. persist the new reading
    ///   3. if a previous existed → run the engine and persist the movement
    ///   4. save
    /// Always compares current vs previous, building a per-match movement time series.
    /// No collection here — readings are handed in by the caller.
    /// </summary>
    public sealed class OddsMovementOrchestrator : IOddsMovementOrchestrator
    {
        private readonly IOddsSnapshotRepository _repository;
        private readonly IOddsMovementEngine _engine;
        private readonly ILogger<OddsMovementOrchestrator> _logger;

        public OddsMovementOrchestrator(
            IOddsSnapshotRepository repository,
            IOddsMovementEngine engine,
            ILogger<OddsMovementOrchestrator> logger)
        {
            _repository = repository;
            _engine = engine;
            _logger = logger;
        }

        public async Task<OddsMovementSnapshot?> IngestAsync(OddsSnapshot newReading, CancellationToken ct = default)
        {
            var previous = await _repository.GetLatestSnapshotAsync(newReading.MatchId, ct);

            await _repository.AddSnapshotAsync(newReading, ct);

            OddsMovementSnapshot? movement = null;
            if (previous is not null)
            {
                movement = _engine.Evaluate(previous, newReading);
                await _repository.AddMovementAsync(movement, ct);
            }

            await _repository.SaveChangesAsync(ct);

            if (movement is not null)
            {
                _logger.LogDebug(
                    "[ODDS ORCH] match {Id}: {Prev} → {Curr} = {Dir}/{Lvl}",
                    newReading.MatchId, movement.PreviousOdds, movement.CurrentOdds,
                    movement.Direction, movement.Level);
            }

            return movement;
        }
    }
}
