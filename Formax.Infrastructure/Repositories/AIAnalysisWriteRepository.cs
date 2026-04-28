using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Formax.Application.Interfaces.Repositories;
using Formax.Domain.States;
using Formax.Infrastructure.Data;
using Formax.Domain.Entities;

namespace Formax.Infrastructure.Repositories
{
    public class AIAnalysisWriteRepository : IAIAnalysisWriteRepository
    {
        private readonly FormaxDbContext _context;

        public AIAnalysisWriteRepository(FormaxDbContext context)
        {
            _context = context;
        }

        // 🔥 FAZ-10 — LAST EXTENDED CONTEXT + TIME PERSIST
        public async Task PersistLastExtendedContextKeyAsync(
            int matchId,
            AIContextKey contextKey,
            CancellationToken cancellationToken)
        {
            var record = await _context.LastExtendedContextKeys
                .FirstOrDefaultAsync(x => x.MatchId == matchId, cancellationToken);

            if (record == null)
            {
                record = new LastExtendedContextKey
                {
                    MatchId = matchId,
                    ContextKey = contextKey,
                    ExtendedAt = DateTime.UtcNow
                };

                _context.LastExtendedContextKeys.Add(record);
            }
            else
            {
                record.ContextKey = contextKey;
                record.ExtendedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
