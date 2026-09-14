using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Application.Interfaces;
using Formax.Application.Services.PostMatch;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Formax.Infrastructure.PostMatch
{
    /// <summary>
    /// ÇALIŞMA ZAMANI KAYNAK LİSTESİ = yayın hakkı tohumları (<see cref="OfficialVideoSources.All"/>) +
    /// DB kataloğundaki DOĞRULANMIŞ kayıtlar. 5 dk önbellek; keşif turu sonrası geçersiz kılınır.
    /// Yalnız arka plan hattı çağırır (keşif, kimlik doğrulama, kayıt kapısı).
    /// </summary>
    public sealed class OfficialVideoSourceCatalog : IOfficialVideoSourceCatalog
    {
        public const string StatusVerified = "Verified";
        public const string StatusCandidate = "Candidate";
        public const string StatusRejected = "Rejected";

        private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);
        private readonly IServiceScopeFactory _scopes;
        private readonly object _gate = new();
        private IReadOnlyList<OfficialVideoSource> _current = OfficialVideoSources.All;
        private DateTime _loadedAt = DateTime.MinValue;

        public OfficialVideoSourceCatalog(IServiceScopeFactory scopes) => _scopes = scopes;

        public void Invalidate() { lock (_gate) _loadedAt = DateTime.MinValue; }

        public IReadOnlyList<OfficialVideoSource> Current()
        {
            lock (_gate)
            {
                if (DateTime.UtcNow - _loadedAt < Ttl) return _current;
                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<FormaxDbContext>();
                var records = db.OfficialVideoSourceCatalog.AsNoTracking()
                    .Where(r => r.Status == StatusVerified).ToList();
                _current = Merge(OfficialVideoSources.All, records);
                _loadedAt = DateTime.UtcNow;
                return _current;
            }
        }

        /// <summary>Tohum + doğrulanmış katalog; aynı kanal/anahtar iki kez girmez (tohum önceliklidir).</summary>
        public static IReadOnlyList<OfficialVideoSource> Merge(IEnumerable<OfficialVideoSource> seeds, IEnumerable<OfficialVideoSourceRecord> records)
        {
            var list = seeds.ToList();
            var channels = new HashSet<string>(list.Where(s => s.YouTubeChannelId != null).Select(s => s.YouTubeChannelId!), StringComparer.Ordinal);
            var keys = new HashSet<string>(list.Select(s => s.Key), StringComparer.OrdinalIgnoreCase);
            foreach (var r in records)
            {
                if (r.Status != StatusVerified || !keys.Add(r.Key)) continue;
                if (r.YouTubeChannelId != null && !channels.Add(r.YouTubeChannelId)) continue;
                list.Add(ToSource(r));
            }
            return list;
        }

        public static OfficialVideoSource ToSource(OfficialVideoSourceRecord r)
            => new(r.Key, r.Publisher, r.Platform, r.YouTubeChannelId, r.AllowsInAppEmbed,
                r.VerificationEvidence, r.Tier, r.ClubName,
                string.IsNullOrWhiteSpace(r.LeagueIds)
                    ? null
                    : r.LeagueIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(x => int.TryParse(x, out var v) ? v : 0).Where(v => v > 0).ToList(),
                r.TeamId);
    }
}
