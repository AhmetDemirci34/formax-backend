using Formax.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.Interfaces
{
    public interface IMatchBanditRepository
    {
        Task<MatchBanditStats> GetOrCreate(int matchId);
        Task IncrementImpression(int matchId);
        Task IncrementLike(int matchId);
    }
}
