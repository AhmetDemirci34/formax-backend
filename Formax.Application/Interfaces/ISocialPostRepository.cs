using System.Collections.Generic;
using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    /// <summary>Phase 7 — Canonical Social Post deposu (AI + UI aynı kaynak).</summary>
    public interface ISocialPostRepository
    {
        /// <summary>Bir maçın sosyal paylaşımları (en yeni önce).</summary>
        List<SocialPost> GetByMatch(string formaxMatchId, int limit = 30);

        /// <summary>ContentHash benzersizliğine göre ekler (varsa atlar). Eklenen sayısını döner.</summary>
        Task<int> UpsertAsync(IEnumerable<SocialPost> posts, CancellationToken ct = default);
    }
}
