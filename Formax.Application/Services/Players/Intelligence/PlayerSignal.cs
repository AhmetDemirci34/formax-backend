namespace Formax.Application.Services.Players.Intelligence
{
    /// <summary>
    /// FORMAX Player Intelligence Engine — tek oyuncu sinyali.
    /// Her sinyal değer + güven + veri-var-mı bilgisini taşır. Eksik veri olduğunda
    /// (Available=false) sistem çalışmaya devam eder (Graceful Fallback).
    /// </summary>
    public sealed class PlayerSignal
    {
        /// <summary>Normalize edilmiş değer (0–100).</summary>
        public double Value { get; set; }

        /// <summary>Bu sinyalin güveni (0–100).</summary>
        public int Confidence { get; set; }

        /// <summary>Veri mevcut mu? false → skorlamada nötr/atlanır.</summary>
        public bool Available { get; set; }

        public static PlayerSignal Missing() => new() { Value = 0, Confidence = 0, Available = false };

        public static PlayerSignal Of(double value, int confidence) =>
            new() { Value = value, Confidence = confidence, Available = true };
    }
}
