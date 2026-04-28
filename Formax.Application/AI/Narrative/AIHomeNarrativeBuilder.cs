using Formax.Application.AI.Narrative.Depth;

namespace Formax.Application.AI.Narrative.Home
{
    public sealed class AIHomeNarrativeBuilder : IHomeNarrativeBuilder
    {
        public string Build(AIContentDepth depth)
        {
            return depth switch
            {
                AIContentDepth.Standard => BuildStandard(),
                _ => BuildSummary()
            };
        }

        private string BuildStandard()
        {
            return "Bugünkü maçlar özelinde dikkat çeken bazı dengeler var. Takımların son haftalardaki oyun eğilimleri, skor beklentilerinden çok maç içi kırılma anlarına işaret ediyor.";
        }

        private string BuildSummary()
        {
            return "Bugün oynanacak maçlar öncesinde temkinli olunması gereken bir gün. Net bir tablo oluşmuş değil.";
        }
    }
}
