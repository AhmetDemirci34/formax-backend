using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Formax.Application.Services.News.Discovery
{
    /// <summary>
    /// FORMAX Data Engine v2 — açık haber kaynağı soyutlaması. Yeni kaynak eklemek =
    /// bu arayüzü implemente eden tek class. Motor provider'ları DI koleksiyonu olarak
    /// alır; biri hata verse sistem durmaz, tek kaynağa bağımlı değildir.
    /// </summary>
    public interface INewsProvider
    {
        string Name { get; }
        bool IsEnabled { get; }

        /// <summary>Verilen maç sorgusu için haber adaylarını döndürür. Hata → boş liste.</summary>
        Task<IReadOnlyList<NewsCandidate>> SearchAsync(NewsQuery query, CancellationToken ct = default);
    }
}
