using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Formax.Domain.Enums;

namespace Formax.Domain.Entities
{
    public class UserNotification
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public int MatchId { get; set; }

        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // ── Takip akışı zenginleştirmesi (geriye uyumlu; legacy satırlar için 0/null) ──

        /// <summary>Bildirim türü — frontend ikon seçimi için.</summary>
        public NotificationEventType EventType { get; set; } = NotificationEventType.Unknown;

        /// <summary>Bildirim kategorisi — frontend segment filtreleme için.</summary>
        public NotificationCategory Category { get; set; } = NotificationCategory.Match;

        /// <summary>Bildirimle ilişkili görsel (takım/lig logosu). Yoksa null.</summary>
        public string? LogoUrl { get; set; }

        /// <summary>İlişkili takım (varsa).</summary>
        public int? TeamId { get; set; }

        /// <summary>İlişkili lig (varsa).</summary>
        public int? LeagueId { get; set; }

        /// <summary>Yönlendirme hedefi tipi.</summary>
        public NotificationTargetType TargetType { get; set; } = NotificationTargetType.Match;

        /// <summary>Yönlendirme hedefi id'si (ör. matchId, newsId). Yoksa MatchId kullanılır.</summary>
        public int? TargetId { get; set; }
    }
}

