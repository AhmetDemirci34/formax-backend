using System;
using System.Collections.Generic;
using Formax.Infrastructure.Providers.Abstractions;

namespace Formax.Infrastructure.Providers.Scheduling;

/// <summary>
/// Merkezi sağlayıcı zamanlayıcısı sözleşmesi.
/// Zamanlama bilgisini tek noktadan sunar; ileride bir BackgroundService bu arayüz üzerinden
/// hangi zamanlamanın çalışması gerektiğini okuyacaktır. Kendisi timer/loop içermez.
/// </summary>
public interface IProviderScheduler
{
    /// <summary>Etkin zamanlamalar, öncelik sırasına göre.</summary>
    IReadOnlyList<ProviderSchedule> GetActiveSchedules();

    /// <summary>Bir provider+capability için zamanlama; yoksa null.</summary>
    ProviderSchedule? GetSchedule(string providerName, ProviderCapability capability);

    /// <summary>
    /// Belirtilen anda (<paramref name="asOfUtc"/>) çalışması gereken zamanlamalar.
    /// Gelecekteki BackgroundService bu metodu döngüsünde çağıracaktır.
    /// </summary>
    IReadOnlyList<ProviderSchedule> GetDueSchedules(DateTimeOffset asOfUtc);
}
