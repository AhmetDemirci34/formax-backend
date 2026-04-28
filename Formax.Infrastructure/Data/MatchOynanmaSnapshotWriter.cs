using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Data
{
    public sealed class MatchOynanmaSnapshotWriter : IMatchOynanmaSnapshotWriter
    {
        private readonly FormaxDbContext _db;
        private static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(30);
        private const string PreMatchFinalSource = "PreMatchFinal";

        public MatchOynanmaSnapshotWriter(FormaxDbContext db)
        {
            _db = db;
        }

        public async Task TryWriteAsync(int matchId, int oynanmaSkoru, DateTime capturedAtUtc, string source, CancellationToken ct = default)
        {
            if (oynanmaSkoru < 0) oynanmaSkoru = 0;
            if (oynanmaSkoru > 100) oynanmaSkoru = 100;

            var last = await _db.MatchOynanmaSnapshots.AsNoTracking()
                .Where(x => x.MatchId == matchId)
                .OrderByDescending(x => x.CapturedAtUtc)
                .FirstOrDefaultAsync(ct);

            if (last != null)
            {
                if ((capturedAtUtc - last.CapturedAtUtc) < MinInterval)
                    return;

                if (last.OynanmaSkoru == oynanmaSkoru)
                    return;
            }

            _db.MatchOynanmaSnapshots.Add(new MatchOynanmaSnapshot
            {
                MatchId = matchId,
                OynanmaSkoru = oynanmaSkoru,
                CapturedAtUtc = capturedAtUtc,
                Source = source
            });

            await _db.SaveChangesAsync(ct);
        }

        public async Task TryWritePreMatchFinalAsync(int matchId, int oynanmaSkoru, DateTime capturedAtUtc, CancellationToken ct = default)
        {
            if (oynanmaSkoru < 0) oynanmaSkoru = 0;
            if (oynanmaSkoru > 100) oynanmaSkoru = 100;

            // Aynı maç için tek kayıt garantisi (Source=PreMatchFinal)
            var alreadyExists = await _db.MatchOynanmaSnapshots.AsNoTracking()
                .AnyAsync(x => x.MatchId == matchId && x.Source == PreMatchFinalSource, ct);

            if (alreadyExists)
                return;

            _db.MatchOynanmaSnapshots.Add(new MatchOynanmaSnapshot
            {
                MatchId = matchId,
                OynanmaSkoru = oynanmaSkoru,
                CapturedAtUtc = capturedAtUtc,
                Source = PreMatchFinalSource
            });

            await _db.SaveChangesAsync(ct);
        }
    }
}
