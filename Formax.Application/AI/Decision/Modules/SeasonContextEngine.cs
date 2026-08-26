using Formax.Application.AI.Context;

namespace Formax.Application.AI.Decision.Modules
{
    /// <summary>
    /// v2.5 MODÜL — Season Context Engine.
    ///
    /// Sezon bağlamını READ-ONLY <see cref="SeasonContextSignals"/> bloğundan yorumlar (Match.MatchDate
    /// türevi — her zaman gerçek). Sezon-sonu stake çarpanını üretir → sezon sonunda maçların önemi
    /// (motivasyon) artar. Stateless & deterministik.
    /// </summary>
    internal sealed class SeasonContextEngine
    {
        public SeasonInsight Analyze(UnifiedMatchAiContext ctx)
        {
            var s = ctx.Season;
            if (s == null || !s.HasData)
                return new SeasonInsight { HasData = false };

            // Sezon-sonu stake: End fazında yüksek, Start'ta düşük.
            var endStake = s.SeasonPhase switch
            {
                "End"    => 0.8,
                "Middle" => 0.3,
                _        => 0.0
            };

            return new SeasonInsight
            {
                HasData     = true,
                SeasonYear  = s.SeasonYear,
                SeasonPhase = s.SeasonPhase,
                EndStake    = endStake,
                Summary     = s.Summary
            };
        }
    }
}
