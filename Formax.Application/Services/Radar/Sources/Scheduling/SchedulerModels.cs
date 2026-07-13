using System;
using System.Collections.Generic;
using Formax.Domain.Entities;
using Formax.Domain.Enums;

namespace Formax.Application.Services.Radar.Sources.Scheduling
{
    /// <summary>
    /// Radar Source Engine (R.8.2) — what the scheduler decided for one source in a cycle.
    /// This sprint plans only; no collection happens, so an outcome of
    /// <see cref="SourceExecutionOutcome.Due"/> means "would be executed now".
    /// </summary>
    public enum SourceExecutionOutcome
    {
        /// <summary>Source is due and would be dispatched to a collector (R.8.3+).</summary>
        Due = 0,

        /// <summary>Not yet due — interval since last run has not elapsed.</summary>
        NotDue = 1,

        /// <summary>Skipped: circuit breaker is Open.</summary>
        SkippedCircuitOpen = 2,

        /// <summary>Skipped: another execution for this source is already in flight.</summary>
        SkippedLocked = 3,

        /// <summary>Skipped: source disabled or inactive (defensive; registry pre-filters).</summary>
        SkippedDisabled = 4
    }

    /// <summary>Per-source result of a single scheduler cycle.</summary>
    public sealed class SourceExecutionResult
    {
        public string SourceKey { get; init; } = string.Empty;
        public SourceCategory Category { get; init; }
        public string FailoverGroup { get; init; } = string.Empty;

        public SourceExecutionOutcome Outcome { get; init; }
        public string Reason { get; init; } = string.Empty;

        public DateTime EvaluatedAtUtc { get; init; }

        /// <summary>Next earliest due time, for diagnostics.</summary>
        public DateTime? NextDueUtc { get; init; }
    }

    /// <summary>
    /// Input row for the planner: a usable source definition plus the runtime facts
    /// the planner needs (circuit state, last run). Assembled by the scheduler from
    /// the registry; the planner itself stays pure and side-effect free.
    /// </summary>
    public sealed class SourceScheduleContext
    {
        public SourceDefinition Definition { get; init; } = default!;
        public SourceCircuitState Circuit { get; init; } = SourceCircuitState.Closed;
        public DateTime? LastRunUtc { get; init; }
    }

    /// <summary>Aggregate result of one scheduler cycle.</summary>
    public sealed class SourceSchedulePlan
    {
        public IReadOnlyList<SourceExecutionResult> Results { get; init; } = new List<SourceExecutionResult>();
        public DateTime PlannedAtUtc { get; init; }
    }
}
