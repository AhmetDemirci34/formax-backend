using Formax.Domain.Entities;
using Formax.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Data
{
    /// <summary>
    /// Synthetic beta dataset (Sprint 15F) — exercises the intelligence layer
    /// (UserTrend / GlobalTrend / Bandit / Confidence) with realistic behaviour
    /// BEFORE real users exist.
    ///
    /// SEPARATE from FormaxSeed and TestBehaviorSeed; idempotent (guarded by the
    /// first beta user). Resolves real matches/teams at run time, so it works
    /// against whatever the provider has synced.
    ///
    /// Shape:
    ///   8 users · 15 team follows (1-2 each) · ~75 actions
    ///   3-4 "popular" matches draw multi-user attention (8-10 actions each)
    ///   long tail: remaining matches get 0-1 action — fallback stays realistic
    ///   action mix: like / skip / detail / save (no single match dominates)
    /// </summary>
    public static class SyntheticBetaSeed
    {
        private const string Marker = "beta_user1@formax.test";

        // ActionType codes: 1=like, -1=skip, 0=view, 2=detail, 3=save(follow-like)
        public static int Seed(FormaxDbContext db)
        {
            try
            {
                if (db.Users.Any(u => u.Email == Marker))
                    return 0; // already seeded

                // Need real matches + teams to attach behaviour to.
                var matches = db.Matches
                    .OrderByDescending(m => m.MatchDate)
                    .Take(40)
                    .ToList();
                if (matches.Count < 6) return 0; // not enough data yet — skip safely

                var teams = db.Teams.ToList();
                if (teams.Count < 8) return 0;

                // Prefer teams that actually appear in the fetched matches.
                var matchTeamIds = matches
                    .SelectMany(m => new[] { m.HomeTeamId, m.AwayTeamId })
                    .Distinct().ToList();
                var pickTeams = teams
                    .Where(t => matchTeamIds.Contains(t.Id))
                    .Take(20).ToList();
                if (pickTeams.Count < 8) pickTeams = teams.Take(20).ToList();

                // ── 8 users ──
                var hasher = new PasswordHashService();
                var users = new List<User>();
                for (var i = 1; i <= 8; i++)
                {
                    users.Add(new User
                    {
                        Email = $"beta_user{i}@formax.test",
                        PasswordHash = hasher.Hash("Test1234!"),
                        IsPremium = i % 4 == 0,
                        CreatedAt = DateTime.UtcNow,
                        FirstSessionCompleted = true,
                        TotalInteractions = 0
                    });
                }
                db.Users.AddRange(users);
                db.SaveChanges(); // assign ids

                // ── 15 team follows (each user 1-2, distinct interests) ──
                var follows = new List<UserTeamFollow>();
                var followPlan = new[] { 2, 2, 2, 2, 2, 2, 2, 1 }; // = 15
                var teamCursor = 0;
                for (var u = 0; u < users.Count; u++)
                {
                    for (var k = 0; k < followPlan[u]; k++)
                    {
                        var team = pickTeams[teamCursor % pickTeams.Count];
                        teamCursor++;
                        follows.Add(new UserTeamFollow
                        {
                            UserId = users[u].Id,
                            TeamId = team.Id,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
                db.UserTeamFollows.AddRange(follows);

                // ── ~75 actions ──
                // Popular cluster: first 4 matches get heavy multi-user attention.
                // Long tail: the rest get sparse single actions.
                var now = DateTime.UtcNow;
                var actions = new List<UserAction>();
                var bandit = new Dictionary<int, (int imp, int like)>();
                (int imp, int like) Zero() => (0, 0);

                void Act(int userId, Match m, int type)
                {
                    var teamName = type % 2 == 0
                        ? null
                        : (m.HomeTeam?.Name); // some actions carry a team, some don't
                    actions.Add(new UserAction
                    {
                        UserId = userId,
                        MatchId = m.Id,
                        ActionType = type,
                        Odds = 1.0,
                        CreatedAt = now.AddMinutes(-actions.Count), // slight recency spread
                        ViewDurationMs = type == 2 ? 8000 : (type == 1 ? 3000 : 500),
                        OpenedDetail = type == 2,
                        Followed = type == 3,
                        Team = teamName
                    });
                    (int imp, int like) b = bandit.TryGetValue(m.Id, out var v) ? v : Zero();
                    b.imp += 1;
                    if (type == 1) b.like += 1;
                    bandit[m.Id] = b;
                }

                // ensure HomeTeam nav for Team name (cheap reload)
                foreach (var m in matches)
                    m.HomeTeam ??= teams.FirstOrDefault(t => t.Id == m.HomeTeamId);

                var rnd = new Random(15062026); // deterministic synthetic spread
                var typeMix = new[] { 1, 1, 1, -1, -1, 2, 2, 3, 0 }; // like-heavy, some skip/detail/save/view

                // Popular cluster — 4 matches, 8-9 distinct-ish actions each (~34)
                var popular = matches.Take(4).ToList();
                foreach (var m in popular)
                {
                    var n = 8 + rnd.Next(0, 2); // 8-9
                    for (var j = 0; j < n; j++)
                    {
                        var user = users[rnd.Next(users.Count)];
                        var type = typeMix[rnd.Next(typeMix.Length)];
                        Act(user.Id, m, type);
                    }
                }

                // Mid tail — next 8 matches, 2-4 actions each (~24)
                foreach (var m in matches.Skip(4).Take(8))
                {
                    var n = 2 + rnd.Next(0, 3);
                    for (var j = 0; j < n; j++)
                        Act(users[rnd.Next(users.Count)].Id, m, typeMix[rnd.Next(typeMix.Length)]);
                }

                // Long tail — remaining matches, 0-1 action (~remainder up to ~75)
                foreach (var m in matches.Skip(12))
                {
                    if (actions.Count >= 75) break;
                    if (rnd.Next(0, 2) == 0) continue; // ~half get nothing
                    Act(users[rnd.Next(users.Count)].Id, m, typeMix[rnd.Next(typeMix.Length)]);
                }

                db.UserActions.AddRange(actions);

                // ── Bandit stats mirror ──
                foreach (var kv in bandit)
                {
                    var s = db.MatchBanditStats.FirstOrDefault(x => x.MatchId == kv.Key);
                    if (s == null)
                    {
                        s = new MatchBanditStats { MatchId = kv.Key, Impressions = 0, Likes = 0, UpdatedAt = now };
                        db.MatchBanditStats.Add(s);
                    }
                    s.Impressions += kv.Value.imp;
                    s.Likes += kv.Value.like;
                    s.UpdatedAt = now;
                }

                db.SaveChanges();
                return actions.Count;
            }
            catch (Exception ex)
            {
                Console.WriteLine("SYNTHETIC BETA SEED ERROR: " + ex.Message);
                return -1;
            }
        }
    }
}
