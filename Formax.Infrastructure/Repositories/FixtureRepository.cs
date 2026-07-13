using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Application.Services.Fixtures;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Repositories
{
    /// <summary>
    /// FORMAX Data Engine v1 — Fixtures upsert. Mevcut kaydı FORMAX_MATCH_ID ile bulur;
    /// saat/durum/güven/kaynak değişmişse günceller, yoksa ekler. Tek SaveChanges.
    /// </summary>
    public sealed class FixtureRepository : IFixtureRepository
    {
        private readonly FormaxDbContext _context;

        public FixtureRepository(FormaxDbContext context)
        {
            _context = context;
        }

        public async Task<(int Added, int Updated)> UpsertAsync(
            IEnumerable<FixtureDiscoveryResult> fixtures, CancellationToken ct = default)
        {
            var incoming = fixtures
                .GroupBy(f => f.FormaxMatchId)        // aynı ID birden çoksa en yükseği al
                .Select(g => g.OrderByDescending(x => x.Confidence).First())
                .ToList();
            if (incoming.Count == 0) return (0, 0);

            var ids = incoming.Select(f => f.FormaxMatchId).ToList();
            var existing = await _context.Fixtures
                .Where(f => ids.Contains(f.FormaxMatchId))
                .ToDictionaryAsync(f => f.FormaxMatchId, ct);

            var now = DateTime.UtcNow;
            int added = 0, updated = 0;

            foreach (var f in incoming)
            {
                var sources = string.Join(",", f.Sources);

                if (existing.TryGetValue(f.FormaxMatchId, out var row))
                {
                    // Sadece anlamlı değişiklikte güncelle (saat / durum / güven / kaynak).
                    if (row.KickoffUtc != f.KickoffUtc || row.Status != f.Status ||
                        row.Confidence != f.Confidence || row.Sources != sources)
                    {
                        row.KickoffUtc = f.KickoffUtc;
                        row.Status = f.Status;
                        row.Confidence = f.Confidence;
                        row.Sources = sources;
                        row.League = f.League;
                        row.Country = f.Country;
                        row.Season = f.Season;
                        row.Round = f.Round;
                        row.Venue = f.Venue;
                        row.UpdatedAt = now;
                        updated++;
                    }
                }
                else
                {
                    _context.Fixtures.Add(new Fixture
                    {
                        FormaxMatchId = f.FormaxMatchId,
                        Country = f.Country,
                        League = f.League,
                        Season = f.Season,
                        Round = f.Round,
                        KickoffUtc = f.KickoffUtc,
                        HomeTeam = f.HomeTeam,
                        AwayTeam = f.AwayTeam,
                        Venue = f.Venue,
                        Status = f.Status,
                        Confidence = f.Confidence,
                        Sources = sources,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                    added++;
                }
            }

            await _context.SaveChangesAsync(ct);
            return (added, updated);
        }

        public async Task<List<Fixture>> GetActiveAsync(DateTime fromUtc, DateTime toUtc,
            CancellationToken ct = default)
        {
            return await _context.Fixtures
                .AsNoTracking()
                .Where(f => f.Status != "Finished" && f.KickoffUtc >= fromUtc && f.KickoffUtc <= toUtc)
                .OrderBy(f => f.KickoffUtc)
                .ToListAsync(ct);
        }
    }
}
