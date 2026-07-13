using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Coverage;
using Formax.Infrastructure.Pipeline.Abstractions;

namespace Formax.Infrastructure.Pipeline.Stages;

/// <summary>
/// 6. aşama — Coverage. <see cref="CoverageEngine"/>'i DI'dan alır.
/// İSKELET: gerçek doluluk hesabı yok; entegrasyon noktası hazır.
/// </summary>
public sealed class CoverageStage : IPipelineStage
{
    private readonly CoverageEngine _engine;

    public CoverageStage(CoverageEngine engine)
    {
        _engine = engine;
    }

    public PipelineStage Stage => PipelineStage.Coverage;

    public Task ExecuteAsync(PipelineContext context, CancellationToken cancellationToken = default)
    {
        context.Set($"gdp:{Stage}", _engine.GetType().Name);
        return Task.CompletedTask;
    }
}
