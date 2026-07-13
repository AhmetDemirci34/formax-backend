using Formax.Application.DTOs.Nabiz;

namespace Formax.Application.Interfaces;

/// <summary>
/// Abstraction over all news/RSS source adapters.
/// Phase A: backed by NabizRssFeedFetcher (multi-source RSS aggregator).
/// Phase B: extend with social / paid-API implementations.
/// </summary>
public interface INabizFeedFetcher
{
    /// <summary>
    /// Fetch raw items from all configured sources.
    /// Each source failure is caught internally — always returns a (possibly empty) list.
    /// </summary>
    Task<List<NabizRawItem>> FetchAllAsync(CancellationToken ct = default);
}
