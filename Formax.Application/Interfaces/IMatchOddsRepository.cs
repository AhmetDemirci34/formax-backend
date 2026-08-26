using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    /// <summary>
    /// GERÇEK market oranlarının kalıcılığı. Yalnız sağlayıcıdan gelen değerler yazılır;
    /// bu depo hiçbir oran türetmez/hesaplamaz.
    /// </summary>
    public interface IMatchOddsRepository
    {
        /// <summary>
        /// Bir maçın normalize market oranlarını yazar/günceller. Var olan (MatchId, MarketKey)
        /// satırı güncellenir ve eski değer PreviousOdd'a taşınır (hareket yönü için).
        /// Geri dönüş: eklenen + güncellenen satır sayısı.
        /// </summary>
        Task<int> UpsertAsync(
            int matchId,
            IReadOnlyDictionary<string, (decimal Odd, int BookmakerId, string BookmakerName)> markets,
            CancellationToken ct = default);

        /// <summary>Tek maçın tüm market oranları. Yoksa boş.</summary>
        Task<IReadOnlyList<MatchMarketOdd>> GetByMatchAsync(int matchId, CancellationToken ct = default);

        /// <summary>Çoklu maç için market oranları (feed N+1 önleme).</summary>
        Task<IReadOnlyList<MatchMarketOdd>> GetByMatchIdsAsync(
            IReadOnlyCollection<int> matchIds, CancellationToken ct = default);

        /// <summary>Diagnostik: kaç maçın en az bir gerçek oranı var.</summary>
        Task<int> CountMatchesWithOddsAsync(CancellationToken ct = default);
    }
}
