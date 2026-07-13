using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Application.Services.News.Intelligence;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Repositories
{
    /// <summary>
    /// FORMAX Data Engine v2.1 — Evidence Store upsert + context. ContentHash ile tekil
    /// saklar; okuma sırasında Freshness uygular ve MatchIntelligenceContext üretir.
    /// </summary>
    public sealed class MatchEvidenceRepository : IMatchEvidenceRepository
    {
        private readonly FormaxDbContext _context;
        private readonly MatchIntelligenceService _intelligence;
        private readonly FreshnessPolicy _freshness;

        public MatchEvidenceRepository(
            FormaxDbContext context, MatchIntelligenceService intelligence, FreshnessPolicy freshness)
        {
            _context = context;
            _intelligence = intelligence;
            _freshness = freshness;
        }

        public async Task<int> UpsertAsync(string formaxMatchId, IEnumerable<MatchEvidence> evidence,
            CancellationToken ct = default)
        {
            var list = evidence.Where(e => !string.IsNullOrWhiteSpace(e.ContentHash))
                               .GroupBy(e => e.ContentHash).Select(g => g.First()).ToList();
            if (list.Count == 0) return 0;

            var hashes = list.Select(e => e.ContentHash).ToList();
            var existing = (await _context.MatchEvidenceRecords
                .Where(r => hashes.Contains(r.ContentHash))
                .Select(r => r.ContentHash).ToListAsync(ct))
                .ToHashSet(StringComparer.Ordinal);

            var now = DateTime.UtcNow;
            var added = 0;
            foreach (var e in list)
            {
                if (existing.Contains(e.ContentHash)) continue;
                _context.MatchEvidenceRecords.Add(new MatchEvidenceRecord
                {
                    FormaxMatchId = formaxMatchId,
                    Type = Trim(e.Type, 48),
                    Cluster = Trim(e.Cluster, 256),
                    Source = Trim(e.Source, 160),
                    SourceQuality = e.SourceQuality,
                    Confidence = e.Confidence,
                    PublishedUtc = e.PublishedUtc,
                    Headline = Trim(e.Headline, 512),
                    ContentHash = e.ContentHash,
                    CreatedAt = now
                });
                added++;
            }

            await _context.SaveChangesAsync(ct);
            return added;
        }

        public async Task<MatchIntelligenceContext> GetContextAsync(string formaxMatchId,
            CancellationToken ct = default)
        {
            var rows = await _context.MatchEvidenceRecords
                .AsNoTracking()
                .Where(r => r.FormaxMatchId == formaxMatchId)
                .ToListAsync(ct);

            // Okuma sırasında Freshness: TTL'i geçen kanıt düşer.
            var fresh = rows
                .Where(r => _freshness.IsFresh(r.Type, r.PublishedUtc))
                .Select(r => new MatchEvidence
                {
                    FormaxMatchId = r.FormaxMatchId,
                    Type = r.Type,
                    Cluster = r.Cluster,
                    Source = r.Source,
                    SourceQuality = r.SourceQuality,
                    Confidence = r.Confidence,
                    PublishedUtc = r.PublishedUtc,
                    Headline = r.Headline,
                    ContentHash = r.ContentHash
                })
                .ToList();

            return _intelligence.BuildContext(formaxMatchId, fresh);
        }

        private static string Trim(string? s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= max ? s : s[..max];
        }
    }
}
