using Formax.Domain.Entities;

namespace Formax.Application.Services.Radar.Intelligence.Odds
{
    /// <summary>
    /// Radar Odds Movement (R.11.1) — computes the deterministic movement between two
    /// odds readings (home market). Pure; no I/O, no AI.
    /// </summary>
    public interface IOddsMovementEngine
    {
        /// <summary>Compute the movement snapshot from an earlier and a later reading.</summary>
        OddsMovementSnapshot Evaluate(OddsSnapshot previous, OddsSnapshot current);
    }
}
