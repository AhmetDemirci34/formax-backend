using Formax.Application.AI.Context;

namespace Formax.Application.AI.Decision.Modules
{
    /// <summary>
    /// v2.5 MODÜL — Competition Context Engine.
    ///
    /// Maçın önemini/türünü READ-ONLY <see cref="CompetitionContextSignals"/> bloğundan yorumlar
    /// (canonical CompetitionContext'ten). Stake seviyesini (Normal→Elimination) çıkarır → motor
    /// bunu DNA (baskı/kaos) ve motivasyona yansıtır. Kaynak boşsa HasData=false (motor v1 gibi).
    /// Stateless & deterministik.
    /// </summary>
    internal sealed class CompetitionContextEngine
    {
        public CompetitionInsight Analyze(UnifiedMatchAiContext ctx)
        {
            var c = ctx.Competition;
            if (c == null || !c.HasData)
                return new CompetitionInsight { HasData = false };

            var stake = c.Importance switch
            {
                "Elimination" => 1.0,
                "Critical"    => 0.8,
                "High"        => 0.5,
                _             => 0.0
            };

            var summary = c.CompetitionType
                + (string.IsNullOrEmpty(c.Stage) || c.Stage == "Unknown" ? "" : $" — {c.Stage}")
                + (c.CurrentRound > 0 ? $" (tur {c.CurrentRound})" : "")
                + $"; önem {c.Importance}"
                + (c.IsElimination ? ", eleme maçı." : ".");

            return new CompetitionInsight
            {
                HasData         = true,
                CompetitionType = c.CompetitionType,
                Stage           = c.Stage,
                Round           = c.CurrentRound,
                Importance      = c.Importance,
                IsElimination   = c.IsElimination,
                StakeLevel      = stake,
                Summary         = summary
            };
        }
    }
}
