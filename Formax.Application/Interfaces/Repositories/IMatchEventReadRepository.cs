using Formax.Application.DTOs.Live;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Formax.Application.Interfaces
{
    public interface IMatchEventReadRepository
    {
        Task<List<LiveMatchEventDto>> GetByMatchAsync(int matchId);
    }
}
