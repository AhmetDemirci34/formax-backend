using Formax.Application.DTOs.MatchIntelligence;

namespace Formax.Application.Services.MatchIntelligence;

/// <summary>
/// Match Intelligence v2 kompozisyon servisi. Mevcut match detay pipeline'ını (AI narrative dahil)
/// yeniden kullanır, Experience bazlı DTO'ya dönüştürür. Mevcut yapıyı DEĞİŞTİRMEZ.
/// </summary>
public interface IMatchIntelligenceService
{
    Task<MatchIntelligenceDto?> BuildAsync(int matchId, CancellationToken ct = default);
}
