using Formax.Application.Interfaces.Repositories;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Repositories;

public class UserActionRepository : IUserActionRepository
{
    private readonly FormaxDbContext _context;

    // PERF (MVP freeze): RecommendationEngine, 100 aday maçın HER BİRİ için
    // GetByUserIdAsync(userId) çağırıyordu — aynı kullanıcı, aynı sonuç, 100 kez.
    // Repository Scoped olduğu için bu önbellek İSTEK BAŞINA geçerlidir; dönen
    // liste birebir aynıdır, yalnız DB round-trip 100 → 1 olur.
    private readonly Dictionary<int, List<UserAction>> _byUserCache = new();

    public UserActionRepository(FormaxDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(UserAction action)
    {
        await _context.UserActions.AddAsync(action);
        await _context.SaveChangesAsync();

        // Yazım sonrası aynı istek içinde bayat okuma olmasın.
        _byUserCache.Remove(action.UserId);
    }

    // 🔥 EKLENDİ (INTERFACE İÇİN ZORUNLU)
    public async Task<List<UserAction>> GetByUserIdAsync(int userId)
    {
        if (_byUserCache.TryGetValue(userId, out var cached))
            return cached;

        var actions = await _context.UserActions
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        _byUserCache[userId] = actions;
        return actions;
    }

    public async Task<List<UserAction>> GetByMatch(int userId, int matchId)
    {
        return await _context.UserActions
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.MatchId == matchId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(20)
            .ToListAsync();
    }
}