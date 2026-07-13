using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Application.Services.News.Discovery;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Repositories
{
    /// <summary>
    /// FORMAX Data Engine v2 — Match News upsert + context. Haberleri ContentHash ile
    /// tekil saklar; aynı hikâye tekrar yazılmaz. GetContext, saklanan kayıtlardan
    /// MatchNewsContext (totals + clusters + headlines + confidence) üretir.
    /// </summary>
    public sealed class MatchNewsRepository : IMatchNewsRepository
    {
        private readonly FormaxDbContext _context;

        public MatchNewsRepository(FormaxDbContext context)
        {
            _context = context;
        }

        public async Task<int> UpsertAsync(string formaxMatchId, IEnumerable<DedupedNewsItem> items,
            CancellationToken ct = default)
        {
            var list = items.Where(i => !string.IsNullOrWhiteSpace(i.ContentHash))
                            .GroupBy(i => i.ContentHash).Select(g => g.First()).ToList();
            if (list.Count == 0) return 0;

            var hashes = list.Select(i => i.ContentHash).ToList();
            var existing = await _context.MatchNewsArticles
                .Where(a => hashes.Contains(a.ContentHash))
                .Select(a => a.ContentHash)
                .ToListAsync(ct);
            var existingSet = existing.ToHashSet(StringComparer.Ordinal);

            var now = DateTime.UtcNow;
            var added = 0;
            foreach (var i in list)
            {
                if (existingSet.Contains(i.ContentHash)) continue;
                _context.MatchNewsArticles.Add(new MatchNewsArticle
                {
                    FormaxMatchId = formaxMatchId,
                    Headline = Trim(i.Headline, 512),
                    Summary = Trim(i.Summary, 1024),
                    Url = Trim(i.Url, 1024),
                    PublishedUtc = i.PublishedUtc,
                    Sources = Trim(string.Join(",", i.Sources), 512),
                    SourceCount = i.SourceCount,
                    Language = Trim(i.Language, 8),
                    Clusters = Trim(string.Join(",", i.Clusters), 256),
                    Confidence = i.Confidence,
                    ContentHash = i.ContentHash,
                    CreatedAt = now
                });
                added++;
            }

            await _context.SaveChangesAsync(ct);
            return added;
        }

        public async Task<List<MatchNewsArticle>> GetArticlesAsync(
            string formaxMatchId, int limit = 20, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(formaxMatchId))
                return new List<MatchNewsArticle>();

            return await _context.MatchNewsArticles
                .AsNoTracking()
                .Where(a => a.FormaxMatchId == formaxMatchId)
                .OrderByDescending(a => a.PublishedUtc)
                .Take(limit)
                .ToListAsync(ct);
        }

        public async Task<MatchNewsContext> GetContextAsync(string formaxMatchId, CancellationToken ct = default)
        {
            var rows = await _context.MatchNewsArticles
                .AsNoTracking()
                .Where(a => a.FormaxMatchId == formaxMatchId)
                .ToListAsync(ct);

            var clusters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in rows.SelectMany(r => r.Clusters.Split(',', StringSplitOptions.RemoveEmptyEntries)))
            {
                var key = c.Trim();
                clusters[key] = clusters.TryGetValue(key, out var n) ? n + 1 : 1;
            }

            var providers = rows.SelectMany(r => r.Sources.Split(',', StringSplitOptions.RemoveEmptyEntries))
                                .Select(s => s.Trim())
                                .Distinct(StringComparer.OrdinalIgnoreCase).Count();

            return new MatchNewsContext
            {
                FormaxMatchId = formaxMatchId,
                TotalNews = rows.Count,
                TotalProviders = providers,
                Clusters = clusters,
                TopHeadlines = rows.OrderByDescending(r => r.Confidence).Take(5).Select(r => r.Headline).ToList(),
                LatestHeadlines = rows.OrderByDescending(r => r.PublishedUtc).Take(5).Select(r => r.Headline).ToList(),
                Confidence = rows.Count == 0 ? 0 : (int)Math.Round(rows.Average(r => r.Confidence))
            };
        }

        private static string Trim(string? s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= max ? s : s[..max];
        }
    }
}
