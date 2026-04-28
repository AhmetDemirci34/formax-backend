using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Domain.States;
using Formax.Infrastructure.Data;

namespace Formax.Infrastructure.Repositories
{
    public class AIAnalysisReadRepository : IAIAnalysisReadRepository
    {
        private readonly FormaxDbContext _context;

        public AIAnalysisReadRepository(FormaxDbContext context)
        {
            _context = context;
        }

        // 🔹 Mevcut kullanım – DOKUNULMUYOR
        public AIAnalysis? GetLastByCouponId(int couponId)
        {
            return _context.AIAnalyses
                .Where(x => x.CouponId == couponId)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefault();
        }

        // 🔥 FAZ-10 — CONTEXT + TIMESTAMP OKUMA
        public (AIContextKey? ContextKey, DateTime? ExtendedAt)
            GetLastExtendedContext(int matchId)
        {
            var last = _context.LastExtendedContextKeys
                .Where(x => x.MatchId == matchId)
                .OrderByDescending(x => x.ExtendedAt)
                .Select(x => new
                {
                    x.ContextKey,
                    x.ExtendedAt
                })
                .FirstOrDefault();

            if (last == null)
                return (null, null);

            return (last.ContextKey, last.ExtendedAt);
        }
    }
}
