using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Formax.Domain.Entities
{
    public class Coupon
    {
        public int Id { get; set; }               // Primary Key

        public int UserId { get; set; }           // Şimdilik sabit / ileride auth

        public string Status { get; set; } = null!;
        // Active | Finished

        public DateTime CreatedAt { get; set; }   // DB tarafından set edilir

        public List<CouponItem> Items { get; set; } = new(); // Navigation

        public string? Result { get; set; }  // Success | Failed
        public string AIComment { get; set; } = string.Empty;
    }
}
