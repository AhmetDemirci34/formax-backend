using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    public interface IAiSpeakTelemetryRepository
    {
        void Add(AiSpeakTelemetry telemetry);
        Task<List<AiSpeakTelemetry>> GetAllAsync();
    }
}
