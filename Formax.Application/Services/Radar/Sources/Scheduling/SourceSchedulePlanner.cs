using System;
using System.Collections.Generic;
using Formax.Application.Interfaces;
using Formax.Domain.Enums;

namespace Formax.Application.Services.Radar.Sources.Scheduling
{
    /// <summary>
    /// Radar Source Engine (R.8.2) — default pure planner.
    ///
    /// For each source: skip if circuit Open or disabled, otherwise compute whether
    /// (now - lastRun) has reached the category interval. A small deterministic jitter
    /// (derived from the source key) is subtracted from the threshold so sources in the
    /// same category do not all become due on exactly the same tick.
    /// </summary>
    public sealed class SourceSchedulePlanner : ISourceSchedulePlanner
    {
        public SourceSchedulePlan Plan(
            IReadOnlyList<SourceScheduleContext> contexts,
            IReadOnlyDictionary<SourceCategory, TimeSpan> categoryIntervals,
            TimeSpan jitter,
            DateTime nowUtc)
        {
            var results = new List<SourceExecutionResult>(contexts.Count);

            foreach (var ctx in contexts)
            {
                var def = ctx.Definition;

                if (!def.Enabled)
                {
                    results.Add(Make(def, SourceExecutionOutcome.SkippedDisabled,
                        "source disabled", nowUtc, null));
                    continue;
                }

                if (ctx.Circuit == SourceCircuitState.Open)
                {
                    results.Add(Make(def, SourceExecutionOutcome.SkippedCircuitOpen,
                        "circuit open", nowUtc, null));
                    continue;
                }

                var interval = categoryIntervals.TryGetValue(def.Category, out var iv)
                    ? iv
                    : TimeSpan.FromMinutes(15); // safe default

                // Deterministic per-source jitter in [0, jitter).
                var jitterOffset = ComputeJitter(def.SourceKey, jitter);
                var effectiveInterval = interval - jitterOffset;
                if (effectiveInterval < TimeSpan.Zero)
                    effectiveInterval = TimeSpan.Zero;

                if (ctx.LastRunUtc is null)
                {
                    // Never run → due immediately.
                    results.Add(Make(def, SourceExecutionOutcome.Due,
                        "first run", nowUtc, nowUtc));
                    continue;
                }

                var elapsed = nowUtc - ctx.LastRunUtc.Value;
                if (elapsed >= effectiveInterval)
                {
                    results.Add(Make(def, SourceExecutionOutcome.Due,
                        $"interval elapsed ({elapsed.TotalSeconds:F0}s ≥ {effectiveInterval.TotalSeconds:F0}s)",
                        nowUtc, nowUtc));
                }
                else
                {
                    var nextDue = ctx.LastRunUtc.Value + effectiveInterval;
                    results.Add(Make(def, SourceExecutionOutcome.NotDue,
                        $"not due ({elapsed.TotalSeconds:F0}s < {effectiveInterval.TotalSeconds:F0}s)",
                        nowUtc, nextDue));
                }
            }

            return new SourceSchedulePlan
            {
                Results = results,
                PlannedAtUtc = nowUtc
            };
        }

        private static SourceExecutionResult Make(
            Domain.Entities.SourceDefinition def,
            SourceExecutionOutcome outcome,
            string reason,
            DateTime nowUtc,
            DateTime? nextDue)
            => new()
            {
                SourceKey = def.SourceKey,
                Category = def.Category,
                FailoverGroup = def.FailoverGroup,
                Outcome = outcome,
                Reason = reason,
                EvaluatedAtUtc = nowUtc,
                NextDueUtc = nextDue
            };

        /// <summary>Stable jitter in [0, max) derived from the source key hash.</summary>
        private static TimeSpan ComputeJitter(string sourceKey, TimeSpan max)
        {
            if (max <= TimeSpan.Zero) return TimeSpan.Zero;

            unchecked
            {
                uint h = 2166136261u;
                foreach (var c in sourceKey)
                {
                    h ^= c;
                    h *= 16777619u;
                }
                var fraction = (h % 1000) / 1000.0; // 0.000 .. 0.999
                return TimeSpan.FromTicks((long)(max.Ticks * fraction));
            }
        }
    }
}
