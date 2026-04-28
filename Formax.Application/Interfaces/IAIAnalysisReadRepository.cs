using Formax.Domain.Entities;
using Formax.Domain.States;

namespace Formax.Application.Interfaces
{
    public interface IAIAnalysisReadRepository
    {
        // 🔹 Mevcut kullanım – DOKUNULMUYOR
        AIAnalysis? GetLastByCouponId(int couponId);

        // 🔥 FAZ-10 — CONTEXT + ZAMAN
        (AIContextKey? ContextKey, DateTime? ExtendedAt)
            GetLastExtendedContext(int matchId);
    }
}
