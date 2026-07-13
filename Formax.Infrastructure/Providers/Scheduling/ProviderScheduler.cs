using System;
using System.Collections.Generic;
using Formax.Infrastructure.Providers.Abstractions;

namespace Formax.Infrastructure.Providers.Scheduling;

/// <summary>
/// Merkezi sağlayıcı zamanlayıcısı.
/// Tüm zamanlama bilgisini <see cref="ProviderScheduleRegistry"/>'den okuyup tek noktadan sunar.
///
/// Bu faz İSKELET: gerçek "due" hesabı (RefreshInterval + son çalışma zamanı karşılaştırması),
/// timer/loop ve provider çalıştırma YAZILMAMIŞTIR. Bir sonraki fazda bir BackgroundService bu
/// zamanlayıcıyı kullanarak döngüyü kuracaktır.
///
/// KAPSAM DIŞI: timer, gerçek zamanlama algoritması, provider çalıştırma, DB — burada YOKTUR.
/// Tasarım BackgroundService dostudur: durumsuz, thread-safe registry'den okur, singleton çözülür.
/// </summary>
public sealed class ProviderScheduler : IProviderScheduler
{
    private readonly ProviderScheduleRegistry _registry;

    public ProviderScheduler(ProviderScheduleRegistry registry)
    {
        _registry = registry;
    }

    public IReadOnlyList<ProviderSchedule> GetActiveSchedules() => _registry.Enabled();

    public ProviderSchedule? GetSchedule(string providerName, ProviderCapability capability) =>
        _registry.TryGet(providerName, capability, out var schedule) ? schedule : null;

    public IReadOnlyList<ProviderSchedule> GetDueSchedules(DateTimeOffset asOfUtc)
    {
        // İSKELET: merkezi altyapı hazır. Gerçek "due" seçimi (etkin zamanlamalar arasından
        // RefreshInterval + son çalışma zamanına göre süzme) ve döngü, BackgroundService fazında.
        // Şimdilik hiçbir zamanlama "due" sayılmaz (hiçbir provider çalıştırılmaz).
        return Array.Empty<ProviderSchedule>();
    }
}
