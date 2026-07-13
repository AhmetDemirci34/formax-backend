using Formax.Domain.Entities;
using Formax.Domain.Constants;
using Formax.Infrastructure.Services;

namespace Formax.Infrastructure.Data
{
    public static class FormaxSeed
    {
        public static void Seed(FormaxDbContext context)
        {
            try
            {
                // --------------------------------------------------
                // TEAM SEED
                // --------------------------------------------------
                if (!context.Teams.Any())
                {
                    context.Teams.AddRange(
                        new Team { Name = "Galatasaray", LeagueRank = 1, AvgGoalsFor = 2.2, AvgGoalsAgainst = 0.8, IsStableTeam = true,  CreatedAt = DateTime.UtcNow },
                        new Team { Name = "Fenerbahçe",  LeagueRank = 2, AvgGoalsFor = 2.0, AvgGoalsAgainst = 1.0, IsStableTeam = true,  CreatedAt = DateTime.UtcNow },
                        new Team { Name = "Beşiktaş",    LeagueRank = 3, AvgGoalsFor = 1.4, AvgGoalsAgainst = 1.4, IsStableTeam = false, CreatedAt = DateTime.UtcNow },
                        new Team { Name = "Trabzonspor", LeagueRank = 4, AvgGoalsFor = 1.1, AvgGoalsAgainst = 1.8, IsStableTeam = false, CreatedAt = DateTime.UtcNow }
                    );

                    context.SaveChanges();
                }

                // --------------------------------------------------
                // MATCH SEED
                // --------------------------------------------------
                if (!context.Matches.Any())
                {
                    var teams = context.Teams.ToList();

                    if (teams.Count < 4)
                        throw new Exception("Team seed başarısız, match üretilemedi");

                    var now = DateTime.UtcNow;

                    var matches = new List<Match>
                    {
                        new Match
                        {
                            HomeTeamId = teams[0].Id,
                            AwayTeamId = teams[1].Id,
                            MatchDate = now.AddMinutes(30),
                            HomeScore = 0,
                            AwayScore = 0,
                            Status = MatchStatuses.PreMatch,
                            CreatedAt = now
                        },
                        new Match
                        {
                            HomeTeamId = teams[1].Id,
                            AwayTeamId = teams[2].Id,
                            MatchDate = now.AddHours(1),
                            HomeScore = 0,
                            AwayScore = 0,
                            Status = MatchStatuses.PreMatch,
                            CreatedAt = now
                        },
                        new Match
                        {
                            HomeTeamId = teams[2].Id,
                            AwayTeamId = teams[3].Id,
                            MatchDate = now.AddHours(2),
                            HomeScore = 0,
                            AwayScore = 0,
                            Status = MatchStatuses.PreMatch,
                            CreatedAt = now
                        },
                        new Match
                        {
                            HomeTeamId = teams[3].Id,
                            AwayTeamId = teams[0].Id,
                            MatchDate = now.AddHours(3),
                            HomeScore = 0,
                            AwayScore = 0,
                            Status = MatchStatuses.PreMatch,
                            CreatedAt = now
                        },
                        new Match
                        {
                            HomeTeamId = teams[0].Id,
                            AwayTeamId = teams[2].Id,
                            MatchDate = now.AddHours(4),
                            HomeScore = 0,
                            AwayScore = 0,
                            Status = MatchStatuses.PreMatch,
                            CreatedAt = now
                        }
                    };

                    // ── Geçmiş Finished maçlar (intelligence data foundation) ──
                    // Çift devreli, asimetrik skorlu. Güç hiyerarşisi: GS > FB > BJK > TS.
                    // Tüm tarihler geçmişte (now-105dk filtresinden geçer).
                    // teams[0]=GS, teams[1]=FB, teams[2]=BJK, teams[3]=TS
                    const string lig = "Türkiye - Süper Lig";

                    Match Finished(int homeIdx, int awayIdx, int hs, int @as, int daysAgo) => new Match
                    {
                        HomeTeamId = teams[homeIdx].Id,
                        AwayTeamId = teams[awayIdx].Id,
                        MatchDate  = now.AddDays(-daysAgo),
                        HomeScore  = hs,
                        AwayScore  = @as,
                        Status     = MatchStatuses.Finished,
                        League     = lig,
                        CreatedAt  = now
                    };

                    var finishedMatches = new List<Match>
                    {
                        Finished(0, 1, 2, 1,  7),  // GS 2-1 FB
                        Finished(0, 2, 3, 0, 14),  // GS 3-0 BJK
                        Finished(0, 3, 2, 0, 21),  // GS 2-0 TS
                        Finished(1, 0, 1, 1, 28),  // FB 1-1 GS
                        Finished(1, 2, 2, 1, 35),  // FB 2-1 BJK
                        Finished(1, 3, 3, 1, 42),  // FB 3-1 TS
                        Finished(2, 0, 0, 2, 49),  // BJK 0-2 GS
                        Finished(2, 1, 1, 1, 56),  // BJK 1-1 FB
                        Finished(2, 3, 2, 1, 63),  // BJK 2-1 TS
                        Finished(3, 0, 0, 1, 70),  // TS 0-1 GS
                        Finished(3, 1, 1, 2, 77),  // TS 1-2 FB
                        Finished(3, 2, 1, 1, 84),  // TS 1-1 BJK
                    };

                    context.Matches.AddRange(matches);
                    context.Matches.AddRange(finishedMatches);
                    context.SaveChanges();

                    // ── MatchLiveStats final kayıtları (Finished maçlar için) ──
                    // SaveChanges sonrası finishedMatches Id'leri set edildi.
                    // MatchId = Match.Id (PK), skorlar maçla birebir eşleşir.
                    // MatchVerdict guard'ı (match.live.stats) ve finished skor tablosu bunu okur.
                    var liveStats = finishedMatches.Select(m => new MatchLiveStats
                    {
                        MatchId   = m.Id,
                        HomeScore = m.HomeScore,
                        AwayScore = m.AwayScore,
                        Minute    = 90,
                        Phase     = "FT",
                        UpdatedAt = now
                    }).ToList();

                    context.MatchLiveStats.AddRange(liveStats);
                    context.SaveChanges();
                }

                // --------------------------------------------------
                // USER SEED
                // --------------------------------------------------
                if (!context.Users.Any())
                {
                    var passwordHasher = new PasswordHashService();

                    var user = new User
                    {
                        Email = "ahmet1@formax.com",
                        PasswordHash = passwordHasher.Hash("Test1234!"),
                        IsPremium = true,
                        CreatedAt = DateTime.UtcNow,
                        TotalInteractions = 0,
                        FirstSessionCompleted = false
                    };

                    context.Users.Add(user);
                    context.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("SEED ERROR: " + ex.Message);
                Console.WriteLine(ex.ToString());
                throw;
            }
        }
    }
}