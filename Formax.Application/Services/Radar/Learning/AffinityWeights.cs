namespace Formax.Application.Services.Radar.Learning
{
    /// <summary>
    /// Radar Learning (R.14.3) — fixed weights for the affinity blend. Deterministic; no AI.
    /// </summary>
    public static class AffinityWeights
    {
        // User-match component weights (sum 1.0).
        public const double Team = 0.45;
        public const double League = 0.25;
        public const double Signal = 0.30;

        // Final blend: user interest dominant, objective importance as baseline/floor.
        public const double User = 0.70;
        public const double Importance = 0.30;

        // Small bonus when the user knows BOTH teams (capped).
        public const double BothTeamsBonusFactor = 0.15;
        public const double BothTeamsBonusCap = 15.0;

        // Banding thresholds.
        public const int CriticalAt = 85;
        public const int HighAt = 60;
        public const int MediumAt = 30;
    }
}
