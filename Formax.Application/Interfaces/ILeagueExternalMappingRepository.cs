using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    public interface ILeagueExternalMappingRepository
    {
        List<LeagueExternalMapping> GetAll();

        LeagueExternalMapping? GetByLeagueId(int leagueId);
    }
}
