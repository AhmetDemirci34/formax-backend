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
                        new Team { Name = "Galatasaray", CreatedAt = DateTime.UtcNow },
                        new Team { Name = "Fenerbahçe", CreatedAt = DateTime.UtcNow },
                        new Team { Name = "Beşiktaş", CreatedAt = DateTime.UtcNow },
                        new Team { Name = "Trabzonspor", CreatedAt = DateTime.UtcNow }
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

                    context.Matches.AddRange(matches);
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