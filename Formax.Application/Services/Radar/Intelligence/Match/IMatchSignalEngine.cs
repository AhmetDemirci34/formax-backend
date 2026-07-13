namespace Formax.Application.Services.Radar.Intelligence.Match
{
    /// <summary>
    /// Radar Match Intelligence (R.9.3) — runs the rule set over a match context and
    /// produces the signal result. Pure: no I/O.
    /// </summary>
    public interface IMatchSignalEngine
    {
        MatchSignalResult Evaluate(MatchContextData context);
    }
}
