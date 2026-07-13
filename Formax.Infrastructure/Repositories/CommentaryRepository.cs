using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Repositories
{
    /// <summary>
    /// Radar Commentary (R.12.1) — EF Core persistence for match commentary snapshots.
    /// </summary>
    public sealed class CommentaryRepository : ICommentaryRepository
    {
        private readonly FormaxDbContext _context;

        public CommentaryRepository(FormaxDbContext context)
        {
            _context = context;
        }

        public async Task<MatchCommentarySnapshot?> GetByMatchIdAsync(int matchId, CancellationToken ct = default)
            => await _context.MatchCommentarySnapshots
                .FirstOrDefaultAsync(x => x.MatchId == matchId, ct);

        public async Task UpsertAsync(MatchCommentarySnapshot snapshot, CancellationToken ct = default)
        {
            var existing = await _context.MatchCommentarySnapshots
                .FirstOrDefaultAsync(x => x.MatchId == snapshot.MatchId, ct);

            if (existing is null)
            {
                _context.MatchCommentarySnapshots.Add(snapshot);
                return;
            }

            existing.Headline = snapshot.Headline;
            existing.Summary = snapshot.Summary;
            existing.Tone = snapshot.Tone;
            existing.Visibility = snapshot.Visibility;
            existing.GeneratedAtUtc = snapshot.GeneratedAtUtc;
        }

        public Task SaveChangesAsync(CancellationToken ct = default)
            => _context.SaveChangesAsync(ct);
    }
}
