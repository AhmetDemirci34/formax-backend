namespace Formax.Application.Services.Hero
{
    /// <summary>
    /// HeroConfidence türetiminde kullanılan ağırlıklar. Hardcoded DEĞİL — ayrı yapı;
    /// ileride yapılandırmadan bağlanabilir. HeroSelectionEngine bunları okur.
    /// </summary>
    public sealed class HeroSelectionWeights
    {
        /// <summary>İki Hero'nun ortalama IntelligenceScore ağırlığı.</summary>
        public double HeroScore { get; set; } = 0.5;

        /// <summary>Radar Confidence ağırlığı.</summary>
        public double RadarConfidence { get; set; } = 0.3;

        /// <summary>Match Importance ağırlığı.</summary>
        public double MatchImportance { get; set; } = 0.2;

        public static HeroSelectionWeights Default => new();
    }
}
