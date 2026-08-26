using System;

namespace Formax.Infrastructure.Http
{
    /// <summary>
    /// api-football timezone yardımcıları (MVP Release Hardening — configurable timezone).
    ///
    /// Tek kaynak: appsettings "ApiFootball:Timezone" (varsayılan "Europe/Istanbul").
    /// Hem provider (query parametresi) hem FixtureSyncJob (gün penceresi hesabı) buradan okur.
    ///
    /// .NET 8 (ICU) hem IANA ("Europe/Istanbul") hem Windows ("Turkey Standard Time") id'lerini
    /// çözebilir; çözülemezse UTC'ye düşer ve çağıran taraf loglar. Sahte değer üretmez.
    /// </summary>
    public static class ApiFootballTimeZone
    {
        public const string DefaultId = "Europe/Istanbul";

        /// <summary>Config'ten okunacak anahtar adı.</summary>
        public const string ConfigKey = "ApiFootball:Timezone";

        /// <summary>
        /// api-football çağrılarına gönderilecek timezone string'i (boşsa varsayılan).
        /// </summary>
        public static string ResolveId(string? configured)
            => string.IsNullOrWhiteSpace(configured) ? DefaultId : configured.Trim();

        /// <summary>
        /// Verilen id için <see cref="TimeZoneInfo"/>; çözülemezse null (çağıran UTC'ye düşer + loglar).
        /// </summary>
        public static TimeZoneInfo? TryResolve(string? configured)
        {
            var id = ResolveId(configured);
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
                return null;
            }
            catch (InvalidTimeZoneException)
            {
                return null;
            }
        }
    }
}
