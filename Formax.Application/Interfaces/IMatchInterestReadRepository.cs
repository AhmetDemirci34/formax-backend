using System.Threading.Tasks;

namespace Formax.Application.Interfaces
{
    public interface IMatchInterestReadRepository
    {
        Task<MatchInterestReadModel?> GetByIdAsync(int matchId);
    }

    public sealed class MatchInterestReadModel
    {
        public int MatchId { get; set; }
        public int HomeTeamId { get; set; }
        public string HomeTeamName { get; set; } = string.Empty;
        public int AwayTeamId { get; set; }
        public string AwayTeamName { get; set; } = string.Empty;
        public string LeagueName { get; set; } = string.Empty;
    }
}