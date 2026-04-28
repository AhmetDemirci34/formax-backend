using System;

namespace Formax.Domain.Entities
{
    public class MatchDiscoveryNode
    {
        public Guid Id { get; set; }

        public int MatchId { get; set; }

        public double X { get; set; }

        public double Y { get; set; }

        public double Intensity { get; set; }

        public string Cluster { get; set; } = "";

        public DateTime UpdatedAt { get; set; }
    }
}