using System.Collections.Generic;
using System.Linq;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Repositories
{
    public class OfficialSocialAccountRepository : IOfficialSocialAccountRepository
    {
        private readonly FormaxDbContext _context;

        public OfficialSocialAccountRepository(FormaxDbContext context)
        {
            _context = context;
        }

        public List<OfficialSocialAccount> GetActiveVerified()
            => _context.OfficialSocialAccounts
                .AsNoTracking()
                .Where(x => x.Active && x.Verified)
                .ToList();

        public List<OfficialSocialAccount> GetByExternalTeamIds(IEnumerable<string> externalTeamIds)
        {
            var ids = externalTeamIds.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
            if (ids.Count == 0) return new List<OfficialSocialAccount>();
            return _context.OfficialSocialAccounts
                .AsNoTracking()
                .Where(x => x.Active && x.Verified
                            && x.ExternalTeamId != null && ids.Contains(x.ExternalTeamId))
                .ToList();
        }

        public async Task UpsertAsync(OfficialSocialAccount account, CancellationToken ct = default)
        {
            var existing = await _context.OfficialSocialAccounts
                .FirstOrDefaultAsync(x => x.Platform == account.Platform && x.Handle == account.Handle, ct);

            if (existing == null)
            {
                account.CreatedAt = System.DateTime.UtcNow;
                account.UpdatedAt = account.CreatedAt;
                _context.OfficialSocialAccounts.Add(account);
                return;
            }

            existing.ScopeType      = account.ScopeType;
            existing.ExternalTeamId = account.ExternalTeamId;
            existing.LeagueId       = account.LeagueId;
            existing.FeedUrl        = account.FeedUrl;
            existing.AccountName    = account.AccountName;
            existing.Verified       = account.Verified;
            existing.Active         = account.Active;
            existing.UpdatedAt      = System.DateTime.UtcNow;
        }

        public Task SaveChangesAsync(CancellationToken ct = default)
            => _context.SaveChangesAsync(ct);
    }
}
