using System.Collections.Generic;
using System.Linq;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Repositories
{
    public class SocialPostRepository : ISocialPostRepository
    {
        private readonly FormaxDbContext _context;

        public SocialPostRepository(FormaxDbContext context)
        {
            _context = context;
        }

        public List<SocialPost> GetByMatch(string formaxMatchId, int limit = 30)
        {
            if (string.IsNullOrWhiteSpace(formaxMatchId)) return new List<SocialPost>();
            return _context.SocialPosts
                .AsNoTracking()
                .Where(x => x.FormaxMatchId == formaxMatchId)
                .OrderByDescending(x => x.PublishedUtc)
                .Take(limit)
                .ToList();
        }

        public async Task<int> UpsertAsync(IEnumerable<SocialPost> posts, CancellationToken ct = default)
        {
            var list = posts?.ToList() ?? new List<SocialPost>();
            if (list.Count == 0) return 0;

            var hashes = list.Select(p => p.ContentHash).Where(h => !string.IsNullOrWhiteSpace(h)).Distinct().ToList();
            var existing = await _context.SocialPosts
                .Where(x => hashes.Contains(x.ContentHash))
                .Select(x => x.ContentHash)
                .ToListAsync(ct);
            var existingSet = new HashSet<string>(existing, System.StringComparer.Ordinal);

            var added = 0;
            foreach (var p in list)
            {
                if (string.IsNullOrWhiteSpace(p.ContentHash) || existingSet.Contains(p.ContentHash)) continue;
                p.CreatedAt = System.DateTime.UtcNow;
                _context.SocialPosts.Add(p);
                existingSet.Add(p.ContentHash);
                added++;
            }

            if (added > 0) await _context.SaveChangesAsync(ct);
            return added;
        }
    }
}
