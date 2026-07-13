namespace Formax.Application.Services.Radar.Intelligence.Match
{
    /// <summary>
    /// Radar Match Intelligence (R.9.6) — computes a first-class match importance score
    /// from the enriched context. Independent of the signal engine. Pure: no I/O.
    /// </summary>
    public interface IMatchImportanceEngine
    {
        MatchImportanceResult Evaluate(MatchContextData context);
    }
}
