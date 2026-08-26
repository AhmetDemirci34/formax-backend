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
        /// <summary>N+1 fix: birden çok maçın bandit satırını TEK sorguda getirir, eksikleri TEK save ile
        /// oluşturur. Değerler GetOrCreate ile birebir aynı (skor matematiği DEĞİŞMEZ; yalnız I/O batch'lenir).</summary>
        Task<Dictionary<int, MatchBanditStats>> GetOrCreateMany(IReadOnlyCollection<int> matchIds);
        Task IncrementImpression(int matchId);
        Task IncrementLike(int matchId);
    }
}
