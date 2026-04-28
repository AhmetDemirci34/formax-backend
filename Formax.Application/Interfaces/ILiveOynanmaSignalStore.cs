using System.Threading.Tasks;

namespace Formax.Application.Interfaces
{
    /// <summary>
    /// Canlı oynanma sinyallerini (çoğunluk tarafı + yoğunluk) anlık olarak tutan store.
    /// NOT: Bu store "gerçek entegrasyon" öncesi test / wiring amaçlıdır.
    /// v1.2: InMemory implementasyon ile hızlı doğrulama.
    /// </summary>
    public interface ILiveOynanmaSignalStore
    {
        Task<OynanmaSinyalleri?> GetAsync(int matchId);
        Task UpsertAsync(int matchId, OynanmaSinyalleri sinyaller);
        Task<bool> RemoveAsync(int matchId);
    }
}
