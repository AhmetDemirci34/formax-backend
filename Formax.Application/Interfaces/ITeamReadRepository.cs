using System.Linq;
using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    public interface ITeamReadRepository
    {
        IQueryable<Team> Query();
        Team? GetById(int id);
        Team? GetByName(string name);
    }
}
