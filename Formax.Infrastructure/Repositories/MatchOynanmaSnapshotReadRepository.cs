using System;
using System.Linq;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Repositories
{
    public class MatchOynanmaSnapshotReadRepository : IMatchOynanmaSnapshotReadRepository
    {
        private readonly FormaxDbContext _context;
        private const string PreMatchFinalSource = "PreMatchFinal";

        public MatchOynanmaSnapshotReadRepository(FormaxDbContext context)
        {
            _context = context;
        }

        public IQueryable<MatchOynanmaSnapshot> Query()
        {
            return _context.MatchOynanmaSnapshots.AsNoTracking().AsQueryable();
        }

        public MatchOynanmaSnapshot? GetLatestBeforeMatchUtc(int matchId, DateTime matchUtc)
        {
            return _context.MatchOynanmaSnapshots.AsNoTracking()
                .Where(x => x.MatchId == matchId && x.CapturedAtUtc <= matchUtc)
                .OrderByDescending(x => x.CapturedAtUtc)
                .FirstOrDefault();
        }

        public MatchOynanmaSnapshot? GetPreMatchFinalBeforeMatchUtc(int matchId, DateTime matchUtc)
        {
            return _context.MatchOynanmaSnapshots.AsNoTracking()
                .Where(x => x.MatchId == matchId && x.Source == PreMatchFinalSource && x.CapturedAtUtc <= matchUtc)
                .OrderByDescending(x => x.CapturedAtUtc)
                .FirstOrDefault();
        }
    }
}
