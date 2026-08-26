using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Repositories
{
    /// <inheritdoc cref="IMatchNewsTranslationRepository"/>
    public sealed class MatchNewsTranslationRepository : IMatchNewsTranslationRepository
    {
        private readonly FormaxDbContext _context;

        public MatchNewsTranslationRepository(FormaxDbContext context)
        {
            _context = context;
        }

        public async Task<Dictionary<string, MatchNewsTranslation>> GetAsync(
            IEnumerable<string> contentHashes, string language, CancellationToken ct = default)
        {
            var hashes = contentHashes
                .Where(h => !string.IsNullOrWhiteSpace(h))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (hashes.Count == 0 || string.IsNullOrWhiteSpace(language))
                return new Dictionary<string, MatchNewsTranslation>(StringComparer.Ordinal);

            var lang = language.Trim().ToLowerInvariant();

            var rows = await _context.MatchNewsTranslations
                .AsNoTracking()
                .Where(t => t.Language == lang && hashes.Contains(t.ContentHash))
                .ToListAsync(ct);

            return rows
                .GroupBy(r => r.ContentHash, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        }

        public async Task<int> UpsertAsync(
            IEnumerable<MatchNewsTranslation> translations, CancellationToken ct = default)
        {
            var list = translations
                .Where(t => !string.IsNullOrWhiteSpace(t.ContentHash)
                            && !string.IsNullOrWhiteSpace(t.Language)
                            && !string.IsNullOrWhiteSpace(t.Headline))
                .GroupBy(t => (t.ContentHash, t.Language))
                .Select(g => g.First())
                .ToList();
            if (list.Count == 0) return 0;

            var hashes = list.Select(t => t.ContentHash).ToList();
            var langs = list.Select(t => t.Language).Distinct().ToList();

            var existing = (await _context.MatchNewsTranslations
                    .Where(t => langs.Contains(t.Language) && hashes.Contains(t.ContentHash))
                    .Select(t => new { t.ContentHash, t.Language })
                    .ToListAsync(ct))
                .Select(x => x.ContentHash + "|" + x.Language)
                .ToHashSet(StringComparer.Ordinal);

            var now = DateTime.UtcNow;
            var added = 0;
            foreach (var t in list)
            {
                if (existing.Contains(t.ContentHash + "|" + t.Language)) continue;
                t.CreatedAt = now;
                _context.MatchNewsTranslations.Add(t);
                added++;
            }

            if (added > 0) await _context.SaveChangesAsync(ct);
            return added;
        }
    }
}
