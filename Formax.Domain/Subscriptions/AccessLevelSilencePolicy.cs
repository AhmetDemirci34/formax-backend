using System;

namespace Formax.Domain.Subscriptions
{
    /// <summary>
    /// 🔒 DOMAIN POLICY
    /// AccessLevel'a göre AI sessizlik toleransı belirler.
    /// </summary>
    public static class AccessLevelSilencePolicy
    {
        public static TimeSpan GetMinimumSilenceDuration(AccessLevel accessLevel)
        {
            return accessLevel switch
            {
                AccessLevel.Premium => TimeSpan.FromMinutes(2),
                AccessLevel.Intro => TimeSpan.FromMinutes(6),
                _ => TimeSpan.FromMinutes(12) // Free
            };
        }
    }
}
