using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.DTOs.Notifications
{
    public class AINotificationDto
    {
        public int MatchId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool IsCritical { get; set; }
        public bool IsPremiumContent { get; set; }
    }
}
