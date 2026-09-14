using System;
using System.Threading;
using System.Threading.Tasks;

namespace Formax.Application.Interfaces
{
    /// <summary>
    /// SENKRON (bloklayan) İŞİ THREAD POOL DIŞINDA ÇALIŞTIRIR.
    ///
    /// NEDEN (ölçüldü 14.09.2026): /detail çekirdeği senkron EF + sync-over-async çağrıları içeriyor.
    /// Eşzamanlı istekler thread pool iş parçacıklarını bloklayınca pool saniyede ~1 iş parçacığı
    /// ekliyor (41→77, CPU ≈ %0) ve DB'ye hiç gitmeyen /health bile 12,4 sn bekliyordu. Bloklayan iş
    /// sınırlı sayıda özel iş parçacığında koşar; bekleyen istek pool iş parçacığı TUTMAZ.
    /// </summary>
    public interface IBlockingWorkScheduler
    {
        Task<T> RunAsync<T>(Func<T> work, CancellationToken ct = default);
    }
}
