using System;

namespace Formax.Domain.Subscriptions
{
    /// <summary>
    /// 🔒 DOMAIN POLICY
    /// Kullanıcının premium olup olmadığını belirler.
    /// </summary>
    public static class PremiumStatusPolicy
    {
        public static bool IsPremium(
            Subscription? subscription,
            DateTime utcNow)
        {
            if (subscription == null)
                return false;

            return subscription.IsActive(utcNow);
        }
    }
}

