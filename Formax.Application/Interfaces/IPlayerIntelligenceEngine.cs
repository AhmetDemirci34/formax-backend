using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.Players.Intelligence;

namespace Formax.Application.Interfaces
{
    /// <summary>
    /// FORMAX Player Intelligence Engine. Bir maçın TÜM kadrosu (ilk 11 + yedekler) için
    /// oyuncu-düzeyi zekâ (IntelligenceScore + sinyaller + gerekçeler) üretir.
    /// Radar'ı değiştirmez; Hero/Profile/Comparison/Squad tarafından ortak kullanılır.
    /// </summary>
    public interface IPlayerIntelligenceEngine
    {
        Task<List<PlayerIntelligence>> GetSquadIntelligenceAsync(int matchId, CancellationToken ct = default);
    }
}
