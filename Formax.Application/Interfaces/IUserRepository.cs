using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    public interface IUserRepository
    {
        User? GetByEmail(string email);
        User? GetById(int id);
        void Add(User user);
        void Update(User user);
    }
}
