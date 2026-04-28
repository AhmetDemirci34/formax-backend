using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.DTOs.Home
{
    /// <summary>
    /// Home içerik erişim modeli.
    /// Free / Preview / Premium davranışını taşır.
    /// </summary>
    public sealed class HomeContentAccessDto
    {
        public bool IsPremiumUser { get; set; }

        /// <summary>
        /// İçerik sadece preview olarak mı gösteriliyor
        /// </summary>
        public bool PreviewOnly { get; set; }

        /// <summary>
        /// İçeriğin tamamı premium gerektiriyor mu
        /// </summary>
        public bool RequiresPremium { get; set; }

        /// <summary>
        /// Kilit sebebi
        /// Örn: premium_required
        /// </summary>
        public string? LockReason { get; set; }

        /// <summary>
        /// Kullanıcıya gösterilecek CTA metni
        /// Örn: Premium ile tamamını gör
        /// </summary>
        public string? CallToAction { get; set; }

        /// <summary>
        /// Açılacak hedef alan
        /// Örn: narrative / radar
        /// </summary>
        public string? UnlockTarget { get; set; }
    }
}