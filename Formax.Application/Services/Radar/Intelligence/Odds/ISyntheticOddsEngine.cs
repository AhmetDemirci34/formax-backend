namespace Formax.Application.Services.Radar.Intelligence.Odds
{
    /// <summary>
    /// Radar Odds Movement (R.11.3) — builds a synthetic odds signal from Radar's
    /// internal scores. Pure, deterministic; no AI, no real odds.
    /// </summary>
    public interface ISyntheticOddsEngine
    {
        SyntheticOddsSignal Evaluate(
            int matchId, double interestScore, double newsImpactScore, double importanceScore);
    }
}
