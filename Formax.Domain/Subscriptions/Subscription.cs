using System;

namespace Formax.Domain.Subscriptions
{
    /// <summary>
    /// 🔒 DOMAIN ENTITY
    /// Premium abonelik yaşam döngüsünü temsil eder.
    /// </summary>
    public class Subscription
    {
        public int Id { get; private set; } // 🔒 EF PRIMARY KEY

        public Guid UserId { get; private set; }
        public SubscriptionPeriod Period { get; private set; }
        public DateTime StartDateUtc { get; private set; }
        public DateTime EndDateUtc { get; private set; }

        public bool IsActive(DateTime utcNow)
        {
            return utcNow >= StartDateUtc && utcNow <= EndDateUtc;
        }

        private Subscription() { } // 🔒 EF Core

        public Subscription(
            Guid userId,
            SubscriptionPeriod period,
            DateTime startDateUtc)
        {
            UserId = userId;
            Period = period;
            StartDateUtc = startDateUtc;
            EndDateUtc = startDateUtc.AddMonths((int)period);
        }
    }
}
