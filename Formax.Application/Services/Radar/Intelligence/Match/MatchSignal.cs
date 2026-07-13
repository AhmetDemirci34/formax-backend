using Formax.Domain.Enums;

namespace Formax.Application.Services.Radar.Intelligence.Match
{
    /// <summary>
    /// Radar Match Intelligence (R.9.1) — one derived importance signal. The middle
    /// stage of the pipeline. Weight orders signals; the highest becomes the snapshot's
    /// primary signal.
    /// </summary>
    public sealed class MatchSignal
    {
        public MatchSignalType Type { get; init; }

        /// <summary>Short display label, e.g. "Derbi".</summary>
        public string Label { get; init; } = string.Empty;

        /// <summary>Why this signal fired (deterministic explanation).</summary>
        public string Reason { get; init; } = string.Empty;

        /// <summary>Relative importance 0..100; orders signals within a match.</summary>
        public double Weight { get; init; }

        public static MatchSignal Of(MatchSignalType type, string label, string reason, double weight)
            => new() { Type = type, Label = label, Reason = reason, Weight = weight };
    }
}
