using System.Collections.Generic;
using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    /// <summary>Phase 7 — doğrulanmış resmi sosyal hesap registry deposu.</summary>
    public interface IOfficialSocialAccountRepository
    {
        /// <summary>Aktif + doğrulanmış tüm hesaplar.</summary>
        List<OfficialSocialAccount> GetActiveVerified();

        /// <summary>Takım-kapsamlı hesaplar (external takım id kümesi).</summary>
        List<OfficialSocialAccount> GetByExternalTeamIds(IEnumerable<string> externalTeamIds);

        /// <summary>Platform+Handle'a göre ekle/güncelle (registry seeding).</summary>
        Task UpsertAsync(OfficialSocialAccount account, CancellationToken ct = default);

        Task SaveChangesAsync(CancellationToken ct = default);
    }
}
