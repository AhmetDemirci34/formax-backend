using System;

namespace Formax.Application.Services.Radar.Intelligence.Match
{
    /// <summary>
    /// Radar Match Intelligence (R.9.3) — one named rule. Evaluates the enriched
    /// <see cref="MatchContextData"/> and optionally emits a <see cref="MatchSignal"/>.
    /// Returning null means the rule did not fire. The engine runs all rules and
    /// collects the signals.
    /// </summary>
    public sealed class MatchSignalRule
    {
        public string Name { get; }

        private readonly Func<MatchContextData, MatchSignal?> _evaluate;

        public MatchSignalRule(string name, Func<MatchContextData, MatchSignal?> evaluate)
        {
            Name = name;
            _evaluate = evaluate;
        }

        public MatchSignal? Evaluate(MatchContextData context) => _evaluate(context);
    }
}
