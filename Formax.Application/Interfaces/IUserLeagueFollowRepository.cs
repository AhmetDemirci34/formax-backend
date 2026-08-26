using System.Collections.Generic;
using System.Threading.Tasks;
using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    /// <summary>
    /// Lig takip deposu. UserTeamFollow mimarisiyle uyumludur; tekil follow/unfollow
    /// (soft-active) + aktif liste ve sayım destekler.
    /// </summary>
    public interface IUserLeagueFollowRepository
    {
        Task<List<UserLeagueFollow>> GetActiveByUserAsync(int userId);
        Task<int> CountActiveByUserAsync(int userId);
        Task FollowAsync(int userId, int leagueId);
        Task UnfollowAsync(int userId, int leagueId);
        /// <summary>Ters lookup: bu ligi AKTİF takip eden kullanıcı id'leri (bildirim fan-out için).</summary>
        Task<List<int>> GetUserIdsByLeagueAsync(int leagueId);
    }
}
