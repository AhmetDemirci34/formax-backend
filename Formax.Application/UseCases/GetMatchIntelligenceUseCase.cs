using Formax.Application.DTOs.MatchIntelligence;
using Formax.Application.Services.MatchIntelligence;

namespace Formax.Application.UseCases;

/// <summary>
/// Match Intelligence v2 giriş noktası (mevcut UseCase konvansiyonu: ExecuteAsync).
/// Controller bunu doğrudan enjekte eder — tıpkı GetMatchDetailAIContextUseCase gibi.
/// </summary>
public sealed class GetMatchIntelligenceUseCase
{
    private readonly IMatchIntelligenceService _service;

    public GetMatchIntelligenceUseCase(IMatchIntelligenceService service)
    {
        _service = service;
    }

    public Task<MatchIntelligenceDto?> ExecuteAsync(int matchId, CancellationToken ct = default)
        => _service.BuildAsync(matchId, ct);
}
