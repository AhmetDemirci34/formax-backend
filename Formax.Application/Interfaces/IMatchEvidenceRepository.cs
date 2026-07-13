using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.News.Intelligence;

namespace Formax.Application.Interfaces
{
    /// <summary>
    /// FORMAX Data Engine v2.1 — Evidence Store kalıcılık katmanı. Sinyalleri
    /// FORMAX_MATCH_ID altında tutar (ContentHash ile tekil) ve Reasoning için taze
    /// MatchIntelligenceContext üretir (süresi geçen kanıt okuma sırasında düşer).
    /// </summary>
    public interface IMatchEvidenceRepository
    {
        Task<int> UpsertAsync(string formaxMatchId, IEnumerable<MatchEvidence> evidence, CancellationToken ct = default);

        Task<MatchIntelligenceContext> GetContextAsync(string formaxMatchId, CancellationToken ct = default);
    }
}
