using System.Collections.Generic;
using System.Threading.Tasks;
using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    public interface IUserTeamFollowRepository
    {
        Task<List<UserTeamFollow>> GetActiveByUserAsync(int userId);
        Task SetUserTeamsSyncAsync(int userId, List<int> teamIds);
        /// <summary>Ters lookup: bu takımı AKTİF takip eden kullanıcı id'leri (bildirim fan-out için).</summary>
        Task<List<int>> GetUserIdsByTeamAsync(int teamId);
    }
}