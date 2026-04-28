using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.DTOs.Auth
{
    public class AuthResponseDto
    {
        public int UserId { get; set; }
        public string Token { get; set; } = string.Empty;
        public bool IsPremium { get; set; }
        public string AccessLevel { get; set; } = "Free";
    }
}
