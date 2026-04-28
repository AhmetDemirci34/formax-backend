namespace Formax.Application.Services.Backtest
{
    public sealed class BacktestRunRequest
    {
        public int SampleSize { get; init; } = 200; // önerilen minimum
        public bool RequireSnapshot { get; init; } = false; // true yaparsan snapshot olmayanları atlar
        public int MinSapma { get; init; } = 0; // örn: 75 => sadece yüksek sapma
    }

    public sealed class BacktestBucketSummary
    {
        public string Bucket { get; init; } = "";
        public int MatchCount { get; init; }

        public int HomeWin { get; init; }
        public int AwayWin { get; init; }
        public int Draw { get; init; }

        /// <summary>
        /// "Çoğunluk" tarafının kazanmadığı (kaybettiği) oran.
        /// Denge (50) maçlarında hesaplanmaz.
        /// </summary>
        public double MajorityDidNotWinRate { get; init; }

        public int MissingSnapshotCount { get; init; }
    }

    public sealed class BacktestRunResult
    {
        public int SampleSizeRequested { get; init; }
        public int SampleSizeUsed { get; init; }
        public bool RequireSnapshot { get; init; }
        public int MinSapma { get; init; }

        public BacktestBucketSummary Denge { get; init; } = new();
        public BacktestBucketSummary YanilmaRiski { get; init; } = new();
        public BacktestBucketSummary YuksekSapma { get; init; } = new();

        public string Note { get; init; } = "";
    }
}
