namespace Formax.Domain.ValueObjects
{
    public static class ContextMemoryWeight
    {
        public static double CalculateHoursDecay(
            double initialWeight,
            double hoursPassed)
        {
            if (hoursPassed <= 0)
                return initialWeight;

            var decayFactor = 0.85; // her saat %15 düşüş
            return initialWeight * Math.Pow(decayFactor, hoursPassed);
        }
    }
}
