using System;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Formax.Application.Services.Radar.Intelligence.Commentary
{
    /// <summary>
    /// Radar Commentary (R.12.1) — default service. For each match with an intelligence
    /// snapshot, reads its signals + news snapshot, runs the deterministic engine, and
    /// persists a commentary. Read-only over intelligence/news; writes only commentary.
    /// </summary>
    public sealed class CommentaryService : ICommentaryService
    {
        private readonly IMatchIntelligenceRepository _matchRepo;
        private readonly INewsIntelligenceRepository _newsRepo;
        private readonly ICommentaryEngine _engine;
        private readonly ICommentaryVisibilityEngine _visibilityEngine;
        private readonly ICommentaryRepository _commentaryRepo;
        private readonly ILogger<CommentaryService> _logger;

        public CommentaryService(
            IMatchIntelligenceRepository matchRepo,
            INewsIntelligenceRepository newsRepo,
            ICommentaryEngine engine,
            ICommentaryVisibilityEngine visibilityEngine,
            ICommentaryRepository commentaryRepo,
            ILogger<CommentaryService> logger)
        {
            _matchRepo = matchRepo;
            _newsRepo = newsRepo;
            _engine = engine;
            _visibilityEngine = visibilityEngine;
            _commentaryRepo = commentaryRepo;
            _logger = logger;
        }

        public async Task<int> BuildAsync(DateTime fromUtc, CancellationToken ct = default)
        {
            var ids = await _matchRepo.GetMatchIdsFromAsync(fromUtc, ct);
            var written = 0;

            foreach (var id in ids)
            {
                var intelligence = await _matchRepo.GetByMatchIdAsync(id, ct);
                if (intelligence is null) continue;

                var news = await _newsRepo.GetByMatchIdAsync(id, ct);

                // R.12.2 — decide visibility before generating; Hidden → empty commentary.
                var visibility = _visibilityEngine.Evaluate(
                    intelligence.ImportanceScore,
                    intelligence.NewsImpactLevel,
                    intelligence.SyntheticSignalLevel,
                    intelligence.SignalCount);

                MatchCommentarySnapshot snapshot;
                if (visibility == Formax.Domain.Enums.CommentaryVisibility.Hidden)
                {
                    snapshot = new MatchCommentarySnapshot
                    {
                        MatchId = id,
                        Headline = string.Empty,
                        Summary = string.Empty,
                        Tone = Formax.Domain.Enums.CommentaryTone.Neutral,
                        Visibility = Formax.Domain.Enums.CommentaryVisibility.Hidden,
                        GeneratedAtUtc = DateTime.UtcNow
                    };
                }
                else
                {
                    var result = _engine.Generate(intelligence, news);
                    snapshot = new MatchCommentarySnapshot
                    {
                        MatchId = id,
                        Headline = result.Headline,
                        Summary = result.Summary,
                        Tone = result.Tone,
                        Visibility = visibility,
                        GeneratedAtUtc = DateTime.UtcNow
                    };
                }

                await _commentaryRepo.UpsertAsync(snapshot, ct);
                written++;
            }

            await _commentaryRepo.SaveChangesAsync(ct);
            _logger.LogInformation("[COMMENTARY] built {Count} commentary snapshot(s).", written);
            return written;
        }
    }
}
