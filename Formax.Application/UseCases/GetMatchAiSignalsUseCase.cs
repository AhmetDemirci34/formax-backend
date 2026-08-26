using System.Collections.Generic;
using System.Linq;
using Formax.Application.AI.Context;
using Formax.Application.AI.Signals;
using Formax.Application.DTOs.Matches;
using Formax.Application.Interfaces;

namespace Formax.Application.UseCases
{
    /// <summary>
    /// GDP AI Signal Factory — DIAGNOSTIC/introspection. Bir maç için UnifiedMatchAiContext'i
    /// (gerçek GDP repo'larından) kurar ve AI Signal Factory çıktısını (context.Signals) döner.
    /// Factory çıktıyı YALNIZ context'e yazar; bu use-case yalnız doğrulama için OKUR.
    /// (Recent-form türevli sinyallerin tam hâli /detail akışındadır; buradaki GDP sinyalleri gerçektir.)
    /// </summary>
    public sealed class GetMatchAiSignalsUseCase
    {
        private readonly IMatchReadRepository _matchRepo;
        private readonly ITeamReadRepository _teamRepo;
        private readonly IMatchAiContextBuilder _builder;

        public GetMatchAiSignalsUseCase(
            IMatchReadRepository matchRepo,
            ITeamReadRepository teamRepo,
            IMatchAiContextBuilder builder)
        {
            _matchRepo = matchRepo;
            _teamRepo = teamRepo;
            _builder = builder;
        }

        public object? Execute(int matchId)
        {
            var match = _matchRepo.GetById(matchId);
            if (match == null) return null;

            var homeName = _teamRepo.GetById(match.HomeTeamId)?.Name ?? "Ev sahibi";
            var awayName = _teamRepo.GetById(match.AwayTeamId)?.Name ?? "Deplasman";

            // FAZ 1 — gerçek TeamComparison/H2H/GücSkoru builder tarafından üretilir (boş DTO kalktı).
            var context = _builder.Build(
                match.Id, match.HomeTeamId, match.AwayTeamId, homeName, awayName);

            var signals = context.Signals ?? new List<AiSignal>();
            var q = context.Quality;
            return new
            {
                matchId,
                home = homeName,
                away = awayName,
                context.Version,
                quality = new
                {
                    q.OverallConfidence, q.OverallDataQuality, q.OverallFreshness,
                    q.OverallSourceTrust, q.OverallEvidenceScore,
                    q.TotalSignalCount, q.ActiveSignalCount,
                    conflictSummary = q.ConflictSummary,
                    reasoningHints = q.ReasoningHints,
                    relatedEntities = q.RelatedEntities
                },
                signals = signals.Select(s => new
                {
                    s.Name, s.Category, s.Value, s.Impact, s.Confidence,
                    s.EvidenceScore, s.SourceTrust, s.Freshness, s.DataQuality,
                    conflictStatus = s.ConflictStatus.ToString(),
                    s.Timestamp, s.Reason, s.RelatedEntities, s.HasData
                })
            };
        }
    }
}
