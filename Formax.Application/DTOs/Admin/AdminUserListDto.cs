using System;

namespace Formax.Application.DTOs.Admin
{
    public class AdminUserListDto
    {
        public int UserId { get; set; }
        public string? Email { get; set; }
        public bool IsRegistered { get; set; }
        public bool IsPremium { get; set; }
        public DateTime? PremiumUntil { get; set; }
    }
}
