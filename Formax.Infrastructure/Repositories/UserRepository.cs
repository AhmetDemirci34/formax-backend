using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;

namespace Formax.Infrastructure.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly FormaxDbContext _context;

        public UserRepository(FormaxDbContext context)
        {
            _context = context;
        }

        public User? GetByEmail(string email)
            => _context.Users.FirstOrDefault(x => x.Email == email);

        public User? GetById(int id)
            => _context.Users.Find(id);

        public void Add(User user)
        {
            _context.Users.Add(user);
            _context.SaveChanges();
        }

        public void Update(User user)
        {
            _context.Users.Update(user);
            _context.SaveChanges();
        }
    }
}
