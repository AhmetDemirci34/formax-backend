using Formax.Domain.Entities;
using Formax.Infrastructure.Data;

public class UserConfidenceRepository : IUserConfidenceRepository
{
    private readonly FormaxDbContext _context;

    public UserConfidenceRepository(FormaxDbContext context)
    {
        _context = context;
    }

    public async Task<UserConfidence?> Get(string userId)
    {
        return _context.UserConfidence.FirstOrDefault(x => x.UserId == userId);
    }

    public async Task Add(UserConfidence c)
    {
        await _context.UserConfidence.AddAsync(c);
        await _context.SaveChangesAsync();
    }

    public async Task Update(UserConfidence c)
    {
        _context.UserConfidence.Update(c);
        await _context.SaveChangesAsync();
    }
}
