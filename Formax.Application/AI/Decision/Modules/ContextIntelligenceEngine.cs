using System.Collections.Generic;
using Formax.Application.AI.Context;

namespace Formax.Application.AI.Decision.Modules
{
    /// <summary>
    /// v2 MODÜL — Context Intelligence Engine (orkestratör).
    ///
    /// Maçın bağlam katmanını üretir: Standings Intelligence + Motivation + Derby + Pressure.
    /// Yalnız gerçek Unified AI Context bloklarından okur (GDP/AiSignalFactory'ye dokunmaz). Her alt
    /// motor bağımsız coverage-gated; hiçbir veri yoksa tüm bloklar HasData=false → motor v1 gibi
    /// davranır (backward-compat). Stateless & deterministik.
    /// </summary>
    internal sealed class ContextIntelligenceEngine
    {
        private readonly StandingsIntelligenceEngine _standings = new();
        private readonly MotivationEngine _motivation = new();
        private readonly DerbyEngine _derby = new();
        private readonly PressureEngine _pressure = new();
        private readonly CompetitionContextEngine _competition = new();
        private readonly TournamentEngine _tournament = new();
        private readonly SeasonContextEngine _season = new();

        public ContextIntelligence Analyze(UnifiedMatchAiContext ctx)
        {
            var competition = _competition.Analyze(ctx);
            var tournament = _tournament.Analyze(ctx);
            var season = _season.Analyze(ctx);
            var standings = _standings.Analyze(ctx);
            var motivation = _motivation.Analyze(ctx, standings, competition, season);
            var derby = _derby.Analyze(ctx);
            var pressure = _pressure.Analyze(ctx);

            var explanations = new List<string>();
            if (competition.HasData) explanations.Add($"Müsabaka: {competition.Summary}");
            if (tournament.HasData) explanations.Add($"Turnuva: {tournament.Summary}");
            if (season.HasData) explanations.Add($"Sezon: {season.Summary}");
            if (standings.HasData) explanations.Add($"Sıralama: {standings.Summary}");
            if (motivation.HasData) explanations.Add($"Motivasyon: {motivation.Summary}");
            if (derby.HasData) explanations.Add($"Derbi: {derby.Summary}");
            if (pressure.HasData) explanations.Add($"Baskı: {pressure.Summary}");

            return new ContextIntelligence
            {
                Standings = standings,
                Motivation = motivation,
                Derby = derby,
                Pressure = pressure,
                Competition = competition,
                Tournament = tournament,
                Season = season,
                Explanations = explanations
            };
        }
    }
}
