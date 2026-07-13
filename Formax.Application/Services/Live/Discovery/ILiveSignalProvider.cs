using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Formax.Application.Services.Live.Discovery
{
    /// <summary>
    /// FORMAX Live Data Engine — açık canlı-veri kaynağı soyutlaması. Yeni kaynak eklemek =
    /// bu arayüzü implemente eden tek class (spor haberi RSS, resmi kulüp akışı, federasyon,
    /// sosyal medya...). Orkestratör provider'ları DI koleksiyonu olarak alır; biri hata
    /// verse sistem durmaz, tek kaynağa bağımlı değildir. (INewsProvider ile aynı desen.)
    /// </summary>
    public interface ILiveSignalProvider
    {
        string Name { get; }
        bool IsEnabled { get; }

        /// <summary>Verilen maç için canlı-sinyal adaylarını döndürür. Hata → boş liste.</summary>
        Task<IReadOnlyList<LiveSignalCandidate>> FetchAsync(LiveSignalQuery query, CancellationToken ct = default);
    }
}
