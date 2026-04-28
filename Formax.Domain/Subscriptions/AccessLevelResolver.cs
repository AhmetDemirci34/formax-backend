using System;

namespace Formax.Domain.Subscriptions
{
    /// <summary>
    /// 🔒 DOMAIN POLICY
    /// Kullanıcının erişim seviyesini belirler.
    /// </summary>
    public static class AccessLevelResolver
    {
        public static AccessLevel Resolve(
            Subscription? subscription,
            IntroAccess? introAccess,
            DateTime utcNow)
        {
            if (PremiumStatusPolicy.IsPremium(subscription, utcNow))
                return AccessLevel.Premium;

            if (introAccess != null && introAccess.IsAvailable())
                return AccessLevel.Intro;

            return AccessLevel.Free;
        }
    }
}
