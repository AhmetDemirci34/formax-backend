using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Domain.Subscriptions
{
    /// <summary>
    /// 🔒 KİLİTLİ
    /// Premium abonelik süreleri.
    /// UI / API bu enumu değiştiremez.
    /// </summary>
    public enum SubscriptionPeriod
    {
        OneMonth = 1,
        ThreeMonths = 3,
        SixMonths = 6,
        TwelveMonths = 12
    }
}

