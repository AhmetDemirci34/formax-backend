using System.Threading;
using System.Threading.Tasks;

namespace Formax.Application.Services.Radar.Intelligence.Match
{
    /// <summary>
    /// Radar Match Intelligence (R.9.4) — enriches a built <see cref="MatchContextData"/>
    /// with staged source data (News / Internal signals / Source metadata). Reads
    /// staging only; never writes back. Returns the enrichment and attaches it to the
    /// context.
    /// </summary>
    public interface IMatchContextEnricher
    {
        Task<ContextEnrichmentResult> EnrichAsync(MatchContextData context, CancellationToken ct = default);
    }
}
