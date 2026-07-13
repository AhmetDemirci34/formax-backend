using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Formax.Application.Services.Fixtures
{
    /// <summary>
    /// FORMAX Data Engine v1 — açık fixture kaynağı soyutlaması. Yeni kaynak eklemek =
    /// bu arayüzü implemente eden tek bir sınıf yazmak (ölçeklenebilirlik). Motor
    /// provider'ları DI'dan koleksiyon olarak alır; tek kaynağa bağımlı değildir.
    /// </summary>
    public interface IFixtureProvider
    {
        /// <summary>İnsan-okur kaynak adı (Sources listesinde ve Confidence'ta kullanılır).</summary>
        string Name { get; }

        /// <summary>Bu provider şu an kullanılabilir mi (örn. yapılandırma/anahtar var mı).</summary>
        bool IsEnabled { get; }

        /// <summary>Verilen tarih aralığındaki fixture adaylarını döndürür. Hata → boş liste.</summary>
        Task<IReadOnlyList<FixtureCandidate>> DiscoverAsync(
            DateOnly fromUtc, DateOnly toUtc, CancellationToken ct = default);
    }
}
