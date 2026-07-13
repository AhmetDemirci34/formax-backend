namespace Formax.Application.Services.Radar.Sources.Staging
{
    /// <summary>
    /// Radar Source Engine (R.8.5) — result of staging one normalization output.
    /// </summary>
    public sealed class StagingWriteResult
    {
        public string SourceKey { get; init; } = string.Empty;
        public int WrittenCount { get; init; }
        public StagingWriteOutcome Outcome { get; init; }
        public string? Error { get; init; }

        public static StagingWriteResult Written(string sourceKey, int count)
            => new() { SourceKey = sourceKey, WrittenCount = count, Outcome = StagingWriteOutcome.Written };

        public static StagingWriteResult Empty(string sourceKey)
            => new() { SourceKey = sourceKey, WrittenCount = 0, Outcome = StagingWriteOutcome.Empty };

        public static StagingWriteResult Failed(string sourceKey, string error)
            => new() { SourceKey = sourceKey, WrittenCount = 0, Outcome = StagingWriteOutcome.Failed, Error = error };
    }
}
