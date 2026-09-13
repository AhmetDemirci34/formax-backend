using System;

namespace Formax.Application.DTOs.Notifications
{
    public class UserNotificationDto
    {
        public int Id { get; set; }
        public int MatchId { get; set; }

        public required string Title { get; set; }
        public required string Message { get; set; }

        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }

        // ── Takip akışı alanları (enum'lar string olarak; frontend doğrudan tüketir) ──
        public string EventType { get; set; } = "Unknown";
        public string Category { get; set; } = "Match";
        public string? LogoUrl { get; set; }
        public int? TeamId { get; set; }
        public int? LeagueId { get; set; }
        public string TargetType { get; set; } = "Match";
        public int? TargetId { get; set; }

        // ── Maç bildirimi sözleşmesi (additive) ──
        /// <summary>"MATCH_LINEUP_AVAILABLE" | "MATCH_CRITICAL_UPDATE" | null (eski satır).</summary>
        public string? Type { get; set; }
        /// <summary>Basınca açılacak rota; eski satırda maç rotası türetilir.</summary>
        public string? Route { get; set; }
    }
}
