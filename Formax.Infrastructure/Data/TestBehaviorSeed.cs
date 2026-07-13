using Formax.Domain.Entities;
using Formax.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Data
{
    /// <summary>
    /// Phase 7 validation seed — multi-user behaviour simulation.
    /// SEPARATE from FormaxSeed and fully idempotent: guarded by the presence
    /// of the test users, so it never runs twice and never touches FormaxSeed data.
    ///
    /// Goal: make the recommendation layers observable without real users.
    ///  - GS-FB match draws interest from 4 distinct users  → high global trend + CTR
    ///  - 3 follow relations (u1→GS, u2→FB, u4→BJK)          → FOLLOWED_TEAM reason
    ///  - a single skip on TS-BJK                            → low global trend
    /// Only raw behaviour tables are filled (Users, UserTeamFollows, UserActions,
    /// MatchBanditStats). All intelligence (global score, profile, reason) is
    /// derived at runtime from these rows.
    /// </summary>
    public static class TestBehaviorSeed
    {
        private const string Marker = "sim_user1@formax.test";

        public static void Seed(FormaxDbContext context)
        {
            try
            {
                // Idempotency guard — if the first sim user exists, nothing to do.
                if (context.Users.Any(u => u.Email == Marker))
                    return;

                // Needs the FormaxSeed teams + finished matches to exist.
                var teams = context.Teams.ToList();
                Team? T(string name) => teams.FirstOrDefault(t => t.Name == name);

                var gs = T("Galatasaray");
                var fb = T("Fenerbahçe");
                var bjk = T("Beşiktaş");
                if (gs == null || fb == null || bjk == null)
                    return; // teams not seeded yet — skip safely

                // Resolve finished matches by (home,away) team ids.
                int? MatchId(int homeId, int awayId) => context.Matches
                    .Where(m => m.Status == "Finished"
                                && m.HomeTeamId == homeId && m.AwayTeamId == awayId)
                    .Select(m => (int?)m.Id)
                    .FirstOrDefault();

                var mGsFb = MatchId(gs.Id, fb.Id);   // GS-FB  → popular match
                var mGsTs = MatchId(gs.Id, T("Trabzonspor")?.Id ?? -1);
                var mFbTs = MatchId(fb.Id, T("Trabzonspor")?.Id ?? -1);
                var mBjkFb = MatchId(bjk.Id, fb.Id);
                var mTsBjk = MatchId(T("Trabzonspor")?.Id ?? -1, bjk.Id); // low-interest match
                var mGsBjk = MatchId(gs.Id, bjk.Id);

                if (mGsFb == null)
                    return; // expected finished match missing — skip safely

                // ── Users: 4 new sim users (u1 == existing ahmet1 conceptually, but we
                // create dedicated sim users so we never depend on FormaxSeed's user). ──
                var hasher = new PasswordHashService();
                User NewUser(string email) => new User
                {
                    Email = email,
                    PasswordHash = hasher.Hash("Test1234!"),
                    IsPremium = false,
                    CreatedAt = DateTime.UtcNow,
                    TotalInteractions = 0,
                    FirstSessionCompleted = true
                };

                var u1 = NewUser("sim_user1@formax.test"); // GS follower
                var u2 = NewUser("sim_user2@formax.test"); // FB follower
                var u3 = NewUser("sim_user3@formax.test"); // generic viewer
                var u4 = NewUser("sim_user4@formax.test"); // BJK follower
                var u5 = NewUser("sim_user5@formax.test"); // contrast, low activity

                context.Users.AddRange(u1, u2, u3, u4, u5);
                context.SaveChanges(); // assigns Ids

                // ── Follows (3) ──
                context.UserTeamFollows.AddRange(
                    new UserTeamFollow { UserId = u1.Id, TeamId = gs.Id,  IsActive = true, CreatedAt = DateTime.UtcNow },
                    new UserTeamFollow { UserId = u2.Id, TeamId = fb.Id,  IsActive = true, CreatedAt = DateTime.UtcNow },
                    new UserTeamFollow { UserId = u4.Id, TeamId = bjk.Id, IsActive = true, CreatedAt = DateTime.UtcNow }
                );

                // ── UserActions: diversified, with Team + intent fields ──
                var now = DateTime.UtcNow;
                UserAction A(int userId, int? matchId, int type, string? team,
                             int viewMs = 0, bool detail = false, bool followed = false) => new UserAction
                {
                    UserId = userId,
                    MatchId = matchId ?? 0,
                    ActionType = type,        // 1=like, -1=skip, 0=view, 2=detail
                    Odds = 1.0,
                    CreatedAt = now,
                    ViewDurationMs = viewMs,
                    OpenedDetail = detail,
                    Followed = followed,
                    Team = team
                };

                var actions = new List<UserAction>
                {
                    // u1 (GS): detail on GS-FB + like GS-TS + view GS-BJK
                    A(u1.Id, mGsFb, 2, "Galatasaray", viewMs: 8000, detail: true),
                    A(u1.Id, mGsTs, 1, "Galatasaray"),
                    A(u1.Id, mGsBjk, 0, "Galatasaray"),
                    // u2 (FB): like + detail on GS-FB, like FB-TS
                    A(u2.Id, mGsFb, 1, "Fenerbahçe"),
                    A(u2.Id, mGsFb, 2, "Fenerbahçe", detail: true),
                    A(u2.Id, mFbTs, 1, "Fenerbahçe"),
                    // u3 (generic): detail GS-FB, view GS-BJK
                    A(u3.Id, mGsFb, 2, null, detail: true),
                    A(u3.Id, mGsBjk, 0, null),
                    // u4 (BJK): like BJK-FB, view GS-FB
                    A(u4.Id, mBjkFb, 1, "Beşiktaş"),
                    A(u4.Id, mGsFb, 0, null),
                    // u5 (contrast): single skip on TS-BJK
                    A(u5.Id, mTsBjk, -1, null),
                };
                context.UserActions.AddRange(actions);

                // ── MatchBanditStats: bump impressions/likes to mirror the actions
                // so CTR/UCB reflects the simulated cross-user interest. ──
                void Bump(int? matchId, int impressions, int likes)
                {
                    if (matchId == null) return;
                    var s = context.MatchBanditStats.FirstOrDefault(x => x.MatchId == matchId);
                    if (s == null)
                    {
                        s = new MatchBanditStats { MatchId = matchId.Value, Impressions = 0, Likes = 0, UpdatedAt = now };
                        context.MatchBanditStats.Add(s);
                    }
                    s.Impressions += impressions;
                    s.Likes += likes;
                    s.UpdatedAt = now;
                }

                Bump(mGsFb, impressions: 5, likes: 2);  // popular
                Bump(mGsTs, impressions: 2, likes: 1);
                Bump(mFbTs, impressions: 2, likes: 1);
                Bump(mBjkFb, impressions: 2, likes: 1);
                Bump(mTsBjk, impressions: 2, likes: 0); // low interest

                context.SaveChanges();
            }
            catch (Exception ex)
            {
                Console.WriteLine("TEST BEHAVIOR SEED ERROR: " + ex.Message);
                // Non-fatal: validation seed must never crash startup.
            }
        }
    }
}
