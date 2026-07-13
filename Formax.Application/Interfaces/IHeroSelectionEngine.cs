using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.Hero;

namespace Formax.Application.Interfaces
{
    /// <summary>
    /// HeroSelectionEngine — yeni sinyal üretmez. PlayerIntelligenceEngine'in ürettiği
    /// IntelligenceScore'u okuyarak HomeHero/AwayHero/HeroReasonType/HeroConfidence üretir.
    /// </summary>
    public interface IHeroSelectionEngine
    {
        Task<HeroSelectionResult> SelectAsync(int matchId, CancellationToken ct = default);
    }
}
