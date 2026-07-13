using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.Players.Intelligence;

namespace Formax.Application.Interfaces
{
    /// <summary>
    /// Oyuncu istatistik sağlayıcısı (Form/Rating/Goals/Assists/Minutes…).
    /// Mevcut api-football entegrasyonu genişletilir; yeni ücretli servis eklenmez.
    /// Devre dışı/erişilemez ise null döner → Player Intelligence Engine graceful fallback yapar.
    /// </summary>
    public interface IPlayerStatsProvider
    {
        /// <summary>Sağlayıcı yapılandırılmış/kullanılabilir mi (ör. API anahtarı var mı).</summary>
        bool IsEnabled { get; }

        /// <summary>Oyuncu istatistiklerini isimle (v1) çözer. Bulunamazsa null.</summary>
        Task<PlayerStats?> GetPlayerStatsAsync(
            string playerName, int teamId, CancellationToken ct = default);
    }
}
