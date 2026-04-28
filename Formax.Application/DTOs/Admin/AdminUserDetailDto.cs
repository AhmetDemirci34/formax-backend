using System;

namespace Formax.Application.DTOs.Admin
{
    public class AdminUserDetailDto
    {
        public int UserId { get; set; }
        public string Email { get; set; } = string.Empty;

        public bool IsPremium { get; set; }

        public int MatchesWithAiCount { get; set; }
        public int AiContextShownCount { get; set; }
        public string? LastAiInteractionType { get; set; }

        public bool HasSeenRegisterHint { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
    }
}
