using System;
using System.Collections.Generic;

namespace Formax.Application.Services.Radar.Sources.Collection
{
    /// <summary>
    /// Radar Source Engine (R.8.3) — outcome of one collector run. Collectors never
    /// throw; failure is reported here (the SourceCollectorBase enforces this).
    /// </summary>
    public sealed class SourceCollectorResult
    {
        public string SourceKey { get; init; } = string.Empty;

        public bool Success { get; init; }

        public IReadOnlyList<SourceCollectorItem> Items { get; init; }
            = Array.Empty<SourceCollectorItem>();

        public int ItemCount => Items.Count;

        /// <summary>Error detail when <see cref="Success"/> is false.</summary>
        public string? Error { get; init; }

        public double DurationMs { get; init; }

        public DateTime FetchedAtUtc { get; init; }

        public static SourceCollectorResult Ok(
            string sourceKey,
            IReadOnlyList<SourceCollectorItem> items,
            double durationMs,
            DateTime fetchedAtUtc)
            => new()
            {
                SourceKey = sourceKey,
                Success = true,
                Items = items,
                DurationMs = durationMs,
                FetchedAtUtc = fetchedAtUtc
            };

        public static SourceCollectorResult Fail(
            string sourceKey,
            string error,
            double durationMs,
            DateTime fetchedAtUtc)
            => new()
            {
                SourceKey = sourceKey,
                Success = false,
                Items = Array.Empty<SourceCollectorItem>(),
                Error = error,
                DurationMs = durationMs,
                FetchedAtUtc = fetchedAtUtc
            };
    }
}
