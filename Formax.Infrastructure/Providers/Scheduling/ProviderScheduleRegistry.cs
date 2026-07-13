using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Formax.Infrastructure.Providers.Abstractions;

namespace Formax.Infrastructure.Providers.Scheduling;

/// <summary>
/// Merkezi zamanlama kayıt defteri: tüm <see cref="ProviderSchedule"/>'ları tutar.
/// Thread-safe'dir (ileride BackgroundService tarafından eşzamanlı okunabilmesi için).
/// (provider adı + capability) çifti başına tek zamanlama tutulur (upsert).
/// </summary>
public sealed class ProviderScheduleRegistry
{
    private readonly ConcurrentDictionary<string, ProviderSchedule> _schedules =
        new(StringComparer.Ordinal);

    /// <summary>Zamanlamayı ekler veya (aynı anahtarda) günceller.</summary>
    public void Register(ProviderSchedule schedule)
    {
        if (schedule is null) throw new ArgumentNullException(nameof(schedule));
        _schedules[schedule.Key] = schedule;
    }

    /// <summary>Bir zamanlamayı kaldırır.</summary>
    public bool Remove(string providerName, ProviderCapability capability) =>
        _schedules.TryRemove(ProviderSchedule.BuildKey(providerName, capability), out _);

    /// <summary>Bir provider+capability için zamanlamayı getirir.</summary>
    public bool TryGet(string providerName, ProviderCapability capability, out ProviderSchedule? schedule) =>
        _schedules.TryGetValue(ProviderSchedule.BuildKey(providerName, capability), out schedule);

    /// <summary>Kayıtlı tüm zamanlamalar.</summary>
    public IReadOnlyList<ProviderSchedule> All => _schedules.Values.ToList();

    /// <summary>Toplam zamanlama sayısı.</summary>
    public int Count => _schedules.Count;

    /// <summary>Yalnızca etkin zamanlamalar, önceliğe göre sıralı.</summary>
    public IReadOnlyList<ProviderSchedule> Enabled() =>
        _schedules.Values
            .Where(s => s.Enabled)
            .OrderByDescending(s => s.Priority)
            .ThenBy(s => s.ProviderName, StringComparer.Ordinal)
            .ToList();

    /// <summary>Belirli bir sağlayıcıya ait zamanlamalar.</summary>
    public IReadOnlyList<ProviderSchedule> ForProvider(string providerName) =>
        _schedules.Values
            .Where(s => string.Equals(s.ProviderName, providerName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(s => s.Priority)
            .ToList();

    /// <summary>Belirli bir yeteneğe ait zamanlamalar.</summary>
    public IReadOnlyList<ProviderSchedule> ForCapability(ProviderCapability capability) =>
        _schedules.Values
            .Where(s => s.Capability == capability)
            .OrderByDescending(s => s.Priority)
            .ToList();
}
