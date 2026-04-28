using Formax.Domain.Entities;

namespace Formax.Application.Interfaces;

public interface IAIWeightConfigRepository
{
    Task<AIWeightConfig?> GetActiveAsync();
}