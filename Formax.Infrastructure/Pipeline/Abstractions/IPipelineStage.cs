using System.Threading;
using System.Threading.Tasks;

namespace Formax.Infrastructure.Pipeline.Abstractions;

/// <summary>
/// GDP pipeline'ının tek bir bağımsız aşaması.
/// Aşamalar birbirini doğrudan tanımaz; yalnızca ortak <see cref="PipelineContext"/> üzerinden veri aktarır.
/// </summary>
public interface IPipelineStage
{
    /// <summary>Bu aşamanın kimliği (çalışma sırasını da belirler).</summary>
    PipelineStage Stage { get; }

    /// <summary>Aşamayı çalıştırır; çıktısını <paramref name="context"/>'e yazabilir.</summary>
    Task ExecuteAsync(PipelineContext context, CancellationToken cancellationToken = default);
}
