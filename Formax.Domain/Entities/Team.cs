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

        /// <summary>
        /// Fixture Expansion v2 — takım Timeline'ının (last=N geçmiş + next=N gelecek)
        /// en son ne zaman senkronlandığı. null = hiç senkronlanmadı (Cold Start adayı).
        /// HistoricalSyncJob bu damgaya göre önceliklendirir: önce null'lar (cold-start),
        /// sonra en eski senkronlananlar (Incremental refresh). Yakın zamanda senkronlanan
        /// takımlar RefreshInterval dolana dek atlanır → API kotası korunur.
        /// </summary>
        public DateTime? TimelineSyncedAt { get; set; }

        // 🔥 JSON LOOP ENGELİ
        [JsonIgnore]
        public ICollection<Match> HomeMatches { get; set; } = new List<Match>();

        [JsonIgnore]
        public ICollection<Match> AwayMatches { get; set; } = new List<Match>();
    }
}