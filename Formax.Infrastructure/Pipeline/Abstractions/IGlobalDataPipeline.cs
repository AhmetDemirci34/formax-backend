using System.Threading;
using System.Threading.Tasks;

namespace Formax.Infrastructure.Pipeline.Abstractions;

/// <summary>
/// GDP uçtan uca pipeline'ı: Provider → Normalize → Match Identity → Merge → Conflict → Coverage.
/// Aşamaları sırayla ve İZOLE çalıştırır; bir aşama başarısız olsa bile pipeline çökmez,
/// durum <see cref="PipelineResult"/> ile döner.
/// </summary>
public interface IGlobalDataPipeline
{
    Task<PipelineResult> RunAsync(PipelineContext context, CancellationToken cancellationToken = default);
}
