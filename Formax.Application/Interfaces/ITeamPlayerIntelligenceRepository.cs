using System.Threading;
using System.Threading.Tasks;
using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    /// <summary>
    /// Football Intelligence v1.0 — takım oyuncu-düzeyi zekâsı deposu (per internal TeamId).
    /// TeamProfileSignal repo deseniyle aynı: ingestion upsert eder, context builder GetByTeam ile okur.
    /// </summary>
    public interface ITeamPlayerIntelligenceRepository
    {
        TeamPlayerIntelligence? GetByTeam(int teamId);
        Task UpsertAsync(TeamPlayerIntelligence entity, CancellationToken ct = default);
    }
}
