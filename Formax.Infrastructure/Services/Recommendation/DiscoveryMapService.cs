using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;

namespace Formax.Infrastructure.Services.Recommendation
{
    public class DiscoveryMapService
    {
        private readonly FormaxDbContext _db;

        public DiscoveryMapService(FormaxDbContext db)
        {
            _db = db;
        }

        public async Task UpdateNode(
            int matchId,
            double radarScore,
            double trendScore,
            double narrativeScore)
        {
            var node = await _db.MatchDiscoveryNodes
                .FirstOrDefaultAsync(x => x.MatchId == matchId);

            if (node == null)
            {
                node = new MatchDiscoveryNode
                {
                    Id = Guid.NewGuid(),
                    MatchId = matchId
                };

                _db.MatchDiscoveryNodes.Add(node);
            }

            node.X = radarScore;
            node.Y = trendScore;

            node.Intensity =
                (radarScore * 0.4) +
                (trendScore * 0.4) +
                (narrativeScore * 0.2);

            node.Cluster = CalculateCluster(node.Intensity);

            node.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
        }

        private string CalculateCluster(double intensity)
        {
            if (intensity > 80)
                return "supernova";

            if (intensity > 60)
                return "hot";

            if (intensity > 40)
                return "warm";

            if (intensity > 20)
                return "cold";

            return "quiet";
        }
    }
}
