using Formax.Domain.Entities;
using System.Collections.Generic;

namespace Formax.Application.Interfaces
{
    public interface IAiSpeakTelemetryReadRepository
    {
        IReadOnlyList<AiSpeakTelemetry> GetAll();
    }
}
