using System;

namespace Formax.Domain.Subscriptions
{
    /// <summary>
    /// 🔒 DOMAIN ENTITY
    /// 6 maçlık tanışma erişimini temsil eder.
    /// Süreye değil, tüketilen maç sayısına bağlıdır.
    /// </summary>
    public class IntroAccess
    {
        public int Id { get; private set; } // 🔒 EF PRIMARY KEY

        public Guid UserId { get; private set; }

        public int MaxMatchCount { get; private set; }
        public int ConsumedMatchCount { get; private set; }
        public bool IsEnabled { get; private set; }

        public bool IsAvailable()
        {
            return IsEnabled && ConsumedMatchCount < MaxMatchCount;
        }

        private IntroAccess() { } // 🔒 EF Core

        public IntroAccess(Guid userId)
        {
            UserId = userId;
            MaxMatchCount = 6;
            ConsumedMatchCount = 0;
            IsEnabled = false; // 🔒 DEFAULT KAPALI
        }

        public void Enable()
        {
            IsEnabled = true;
        }

        public void ConsumeOneMatch()
        {
            if (!IsAvailable())
                throw new InvalidOperationException("Intro access is not available.");

            ConsumedMatchCount++;
        }
    }
}
