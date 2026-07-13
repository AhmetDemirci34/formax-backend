using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Formax.Domain.Entities
{
    public class Team
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public int? LeagueRank { get; set; }

        public double? AvgGoalsFor { get; set; }

        public double? AvgGoalsAgainst { get; set; }

        public bool? IsStableTeam { get; set; }

        public DateTime CreatedAt { get; set; }

        public string? LogoUrl { get; set; }

        public string? ColorPrimary { get; set; }

        public string? ExternalTeamId { get; set; }

        public string? ColorSecondary { get; set; }

        // Sprint 20A — API-Football team mapping
        public int? ApiFootballTeamId { get; set; }

        // 🔥 JSON LOOP ENGELİ
        [JsonIgnore]
        public ICollection<Match> HomeMatches { get; set; } = new List<Match>();

        [JsonIgnore]
        public ICollection<Match> AwayMatches { get; set; } = new List<Match>();
    }
}