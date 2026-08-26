using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Coverage
{
    /// <summary>
    /// Timeline Cold Start Prioritization — HistoricalSyncJob (seçim) ve dashboard preview (kanıt)
    /// için TEK önceliklendirme otoritesi. Kota asla kapsam-dışı/önemsiz takımlara önce harcanmasın:
    /// öncelik sırası (küçük tier = yüksek öncelik):
    ///   1. Bugün maçı olan takımlar
    ///   2. Önümüzdeki 7 gün içinde maçı olan takımlar
    ///   3. MVP kapsamındaki liglerin takımları (Coverage:LeagueAllowList)
    ///   4. Kullanıcıların takip ettiği liglerin takımları (UserLeagueFollow)
    ///   5. Diğer aktif takımlar (gelecekte maçı var ama >7g, ya da yalnız geçmiş aktivite)
    ///   6. Arşiv (hiç yaklaşan maçı olmayan)
    /// Tier içinde: en yakın maç önce; eşitlikte cold-start (hiç senkronlanmamış) refresh'ten önce.
    /// Watermark (TimelineSyncedAt) ile yakın zamanda senkronlananlar ELENİR (kota koruması).
    /// </summary>
    public static class TimelinePriority
    {
        public sealed record RankedTeam(
            int Id, string? ExternalTeamId, string Name, DateTime? SyncedAt,
            int Tier, string Reason, DateTime? NearestMatch);

        /// <summary>
        /// Watermark'a göre uygun takımları önceliğe göre sıralı döndürür. API çağrısı YOK — yalnız DB.
        /// </summary>
        public static List<RankedTeam> RankEligible(
            FormaxDbContext db, DateTime nowUtc, DateTime refreshCutoff,
            HashSet<int> mvpLeagues, HashSet<int> followedLeagues)
        {
            // Uygun takımlar: hiç senkronlanmamış (cold start) VEYA refresh süresi dolmuş (incremental).
            var eligible = db.Teams
                .Where(t => t.ExternalTeamId != null && t.ExternalTeamId != ""
                         && (t.TimelineSyncedAt == null || t.TimelineSyncedAt < refreshCutoff))
                .Select(t => new { t.Id, t.ExternalTeamId, t.Name, t.TimelineSyncedAt })
                .ToList();
            if (eligible.Count == 0) return new List<RankedTeam>();

            var eligibleIds = eligible.Select(e => e.Id).ToHashSet();

            // En yakın YAKLAŞAN maç (takım başına), yalnız uygun takımlar için — hafif iki grup sorgu.
            var nearest = new Dictionary<int, DateTime>();
            void Fold(IEnumerable<(int TeamId, DateTime Date)> rows)
            {
                foreach (var r in rows)
                {
                    if (!eligibleIds.Contains(r.TeamId)) continue;
                    if (!nearest.TryGetValue(r.TeamId, out var cur) || r.Date < cur)
                        nearest[r.TeamId] = r.Date;
                }
            }
            var upHome = db.Matches.Where(m => m.MatchDate >= nowUtc)
                .Select(m => new { T = m.HomeTeamId, m.MatchDate }).ToList()
                .Select(x => (x.T, x.MatchDate));
            var upAway = db.Matches.Where(m => m.MatchDate >= nowUtc)
                .Select(m => new { T = m.AwayTeamId, m.MatchDate }).ToList()
                .Select(x => (x.T, x.MatchDate));
            Fold(upHome);
            Fold(upAway);

            // MVP/takip lig üyeliği yalnız ilgili listeler doluysa hesaplanır (aksi halde gereksiz tarama yok).
            Dictionary<int, HashSet<int>> teamLeagues = null!;
            if (mvpLeagues.Count > 0 || followedLeagues.Count > 0)
            {
                teamLeagues = new Dictionary<int, HashSet<int>>();
                void FoldLeague(IEnumerable<(int TeamId, int League)> rows)
                {
                    foreach (var r in rows)
                    {
                        if (!eligibleIds.Contains(r.TeamId) || r.League <= 0) continue;
                        if (!teamLeagues.TryGetValue(r.TeamId, out var set))
                            teamLeagues[r.TeamId] = set = new HashSet<int>();
                        set.Add(r.League);
                    }
                }
                FoldLeague(db.Matches.Where(m => m.LeagueId > 0)
                    .Select(m => new { T = m.HomeTeamId, m.LeagueId }).ToList().Select(x => (x.T, x.LeagueId)));
                FoldLeague(db.Matches.Where(m => m.LeagueId > 0)
                    .Select(m => new { T = m.AwayTeamId, m.LeagueId }).ToList().Select(x => (x.T, x.LeagueId)));
            }

            var today1 = nowUtc.AddDays(1);
            var horizon7 = nowUtc.AddDays(7);

            var ranked = eligible.Select(e =>
            {
                nearest.TryGetValue(e.Id, out var near);
                bool hasNear = nearest.ContainsKey(e.Id);
                var leagues = teamLeagues != null && teamLeagues.TryGetValue(e.Id, out var ls) ? ls : null;

                int tier; string reason;
                if (hasNear && near < today1)              { tier = 1; reason = "Bugün maçı var"; }
                else if (hasNear && near <= horizon7)       { tier = 2; reason = "7 gün içinde maçı var"; }
                else if (leagues != null && leagues.Overlaps(mvpLeagues)) { tier = 3; reason = "MVP lig takımı"; }
                else if (leagues != null && leagues.Overlaps(followedLeagues)) { tier = 4; reason = "Takip edilen lig takımı"; }
                else if (hasNear)                            { tier = 5; reason = "Aktif (yaklaşan maç >7g)"; }
                else                                         { tier = 6; reason = "Arşiv (yaklaşan maç yok)"; }

                return new RankedTeam(e.Id, e.ExternalTeamId, e.Name, e.TimelineSyncedAt,
                    tier, reason, hasNear ? near : (DateTime?)null);
            });

            // Tier ASC → en yakın maç ASC (yoksa en sona) → cold-start (SyncedAt null) refresh'ten önce.
            return ranked
                .OrderBy(r => r.Tier)
                .ThenBy(r => r.NearestMatch ?? DateTime.MaxValue)
                .ThenBy(r => r.SyncedAt ?? DateTime.MinValue)
                .ToList();
        }
    }
}
