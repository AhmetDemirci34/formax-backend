using System;
using System.Collections.Generic;
using Formax.Application.Services.Radar.Sources.Scheduling;
using Formax.Domain.Enums;

namespace Formax.Application.Interfaces
{
    /// <summary>
    /// Radar Source Engine (R.8.2) — pure decision layer. Given the usable sources
    /// (with their circuit state and last-run) plus the per-category intervals,
    /// decides which sources are due now. No side effects, no I/O.
    /// </summary>
    public interface ISourceSchedulePlanner
    {
        SourceSchedulePlan Plan(
            IReadOnlyList<SourceScheduleContext> contexts,
            IReadOnlyDictionary<SourceCategory, TimeSpan> categoryIntervals,
            TimeSpan jitter,
            DateTime nowUtc);
    }
}
