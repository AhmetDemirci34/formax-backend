using System.Collections.Generic;
using System.Threading.Tasks;
using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    public interface ITeamRepository
    {
        Task<List<Team>> GetAllAsync();
        Task<List<Team>> GetByIdsAsync(List<int> teamIds);
        Team? GetById(int id);
        Team GetByName(string name);
        List<Team> GetAll();
    }
}
