using Formax.Application.Interfaces.Repositories;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Repositories;

public class UserActionRepository : IUserActionRepository
{
    private readonly FormaxDbContext _context;

    public UserActionRepository(FormaxDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(UserAction action)
    {
        await _context.UserActions.AddAsync(action);
        await _context.SaveChangesAsync();
    }

    // 🔥 EKLENDİ (INTERFACE İÇİN ZORUNLU)
    public async Task<List<UserAction>> GetByUserIdAsync(int userId)
    {
        return await _context.UserActions
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<UserAction>> GetByMatch(int userId, int matchId)
    {
        var query = _context.UserActions
            .Where(x => x.UserId == userId && x.MatchId == matchId);

        Console.WriteLine($"[SQL] USER:{userId} MATCH:{matchId}");

        var data = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(20)
            .ToListAsync();

        Console.WriteLine($"[SQL RESULT COUNT]: {data.Count}");

        return data;
    }
}