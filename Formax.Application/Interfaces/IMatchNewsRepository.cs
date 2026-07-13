using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.News.Discovery;
using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    /// <summary>
    /// FORMAX Data Engine v2 — Match News kalıcılık katmanı. Haberleri FORMAX_MATCH_ID
    /// altında saklar (ContentHash ile tekil) ve Reasoning için MatchNewsContext üretir.
    /// </summary>
    public interface IMatchNewsRepository
    {
        /// <summary>Yeni haberleri ekler (ContentHash mevcutsa atlar). Eklenen sayısını döndürür.</summary>
        Task<int> UpsertAsync(string formaxMatchId, IEnumerable<DedupedNewsItem> items, CancellationToken ct = default);

        /// <summary>Saklanan haberlerden maçın haber bağlamını üretir.</summary>
        Task<MatchNewsContext> GetContextAsync(string formaxMatchId, CancellationToken ct = default);

        /// <summary>
        /// Maçın saklanan ham haber makalelerini (en yeni önce) döndürür. Match Intelligence
        /// News Engine'in per-makale analizi (önem/etki/hero) için kullanılır; ikinci bir
        /// toplama yapılmaz — NewsDiscovery'nin zaten yazdığı kayıtlar okunur.
        /// </summary>
        Task<List<MatchNewsArticle>> GetArticlesAsync(string formaxMatchId, int limit = 20, CancellationToken ct = default);
    }
}
