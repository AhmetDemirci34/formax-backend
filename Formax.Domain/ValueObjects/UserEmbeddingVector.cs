using System.Collections.Generic;

namespace Formax.Domain.ValueObjects
{
    public class UserEmbeddingVector
    {
        public Dictionary<int, double> TeamWeights { get; set; } = new();
        public Dictionary<int, double> LeagueWeights { get; set; } = new();

        public double Normalize(double value) => System.Math.Min(1, System.Math.Max(0, value));
    }
}
