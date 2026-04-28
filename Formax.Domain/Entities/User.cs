using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Domain.Entities
{
    public class User
    {
        public int Id { get; set; }

        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;

        public int MatchesWithAiCount { get; set; }
        public int AiContextShownCount { get; set; }
        public DateTime? LastAiInteractionAt { get; set; }
        public string? LastAiInteractionType { get; set; }

        public bool HasSeenRegisterHint { get; set; }
        public bool IsPremium { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public string? LastExtendedContextKey { get; private set; }
        public bool FirstSessionCompleted { get; set; }
        public int TotalInteractions { get; set; }




    }
}
