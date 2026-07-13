namespace Formax.Application.Services.Radar.Intelligence.Match
{
    /// <summary>
    /// Radar Match Intelligence (R.9.6) — one contributing factor to a match's
    /// importance, with the points it added and why. Deterministic; no AI.
    /// </summary>
    public sealed class MatchImportanceFactor
    {
        public string Name { get; init; } = string.Empty;
        public double Points { get; init; }
        public string Reason { get; init; } = string.Empty;

        public static MatchImportanceFactor Of(string name, double points, string reason)
            => new() { Name = name, Points = points, Reason = reason };
    }
}
