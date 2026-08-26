using Formax.Application.AI.Context;

namespace Formax.Application.AI.Decision.Modules
{
    /// <summary>
    /// v2.5 MODÜL — Tournament Engine.
    ///
    /// Eleme/turnuva bağlamını READ-ONLY <see cref="TournamentContextSignals"/> bloğundan yorumlar:
    /// uzatma/penaltı olasılığı, aggregate önemi. Kaynak boşsa (lig maçı / bracket yok) HasData=false.
    /// Stateless & deterministik.
    /// </summary>
    internal sealed class TournamentEngine
    {
        public TournamentInsight Analyze(UnifiedMatchAiContext ctx)
        {
            var t = ctx.Tournament;
            if (t == null || !t.HasData)
                return new TournamentInsight { HasData = false };

            var parts = new System.Collections.Generic.List<string>();
            if (t.AggregateMatters) parts.Add("toplam skor belirleyici");
            if (t.ExtraTimePossible) parts.Add("uzatma olası");
            if (t.PenaltiesPossible) parts.Add("penaltı olası");
            var summary = parts.Count > 0 ? "Eleme: " + string.Join(", ", parts) + "." : "Eleme bağlamı.";

            return new TournamentInsight
            {
                HasData           = true,
                ExtraTimePossible = t.ExtraTimePossible,
                PenaltiesPossible = t.PenaltiesPossible,
                AggregateMatters  = t.AggregateMatters,
                Summary           = summary
            };
        }
    }
}
