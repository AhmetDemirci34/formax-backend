using Formax.Domain.Entities;

namespace Formax.Infrastructure.Data
{
    public static class Seed
    {
        public static void Run(FormaxDbContext context)
        {
            SeedTeams(context);
            SeedPredictionTypes(context);
            SeedMatches(context);
        }

        // --------------------------------------------------
        // TEAMS
        // --------------------------------------------------
        private static void SeedTeams(FormaxDbContext context)
        {
            if (context.Teams.Any())
                return;

            var teams = new List<Team>
            {
                new() { Name = "Galatasaray" },
                new() { Name = "Fenerbahçe" },
                new() { Name = "Beşiktaş" },
                new() { Name = "Trabzonspor" }
            };

            context.Teams.AddRange(teams);
            context.SaveChanges();
        }

        // --------------------------------------------------
        // PREDICTION TYPES
        // --------------------------------------------------
        private static void SeedPredictionTypes(FormaxDbContext context)
        {
            if (context.PredictionTypes.Any())
                return;

            var now = DateTime.UtcNow;

            var types = new List<PredictionType>
            {
                new() { Name = "2.5 Üst", GroupCode = "GOAL", CreatedAt = now},
                new() { Name = "2.5 Alt", GroupCode = "GOAL", CreatedAt = now},

                new() { Name = "Maç Sonucu 1", GroupCode = "RESULT", CreatedAt = now},
                new() { Name = "Maç Sonucu X", GroupCode = "RESULT", CreatedAt = now},
                new() { Name = "Maç Sonucu 2", GroupCode = "RESULT", CreatedAt = now}
            };

            context.PredictionTypes.AddRange(types);
            context.SaveChanges();
        }

        // --------------------------------------------------
        // MATCHES (SPRINT 7 – UPCOMING MATCHES)
        // --------------------------------------------------
        private static void SeedMatches(FormaxDbContext context)
        {
            if (context.Matches.Any())
                return;

            var gs = context.Teams.First(t => t.Name == "Galatasaray");
            var fb = context.Teams.First(t => t.Name == "Fenerbahçe");
            var bjk = context.Teams.First(t => t.Name == "Beşiktaş");
            var ts = context.Teams.First(t => t.Name == "Trabzonspor");

            var now = DateTime.UtcNow;

            var matches = new List<Match>
            {
                new Match
                {
                    HomeTeamId = gs.Id,
                    AwayTeamId = fb.Id,
                    MatchDate = now.AddHours(3),   // 🔥 upcoming
                    Status = "NotStarted",
                    CreatedAt = now,
                    League = "Türkiye - Süper Lig",
                },
                new Match
                {
                    HomeTeamId = bjk.Id,
                    AwayTeamId = ts.Id,
                    MatchDate = now.AddHours(5),   // 🔥 upcoming
                    Status = "NotStarted",
                    CreatedAt = now,
                    League = "Türkiye - Süper Lig",
                }
            };

            context.Matches.AddRange(matches);
            context.SaveChanges();
        }
    }
}