using Microsoft.EntityFrameworkCore;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using System.Globalization;
using System.Text;

namespace Formax.Infrastructure.Repositories;

public class UserInterestScoreRepository : IUserInterestScoreRepository
{
    private readonly FormaxDbContext _context;

    public UserInterestScoreRepository(FormaxDbContext context)
    {
        _context = context;
    }

    // 🔥 NORMALIZE (TEK GERÇEK)
    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();

        foreach (var c in normalized)
        {
            var category = Char.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    // 🔹 GET
    public async Task<UserInterestScore?> GetAsync(int userId, string layer, string key)
    {
        layer = Normalize(layer);
        key = Normalize(key);

        return await _context.Set<UserInterestScore>()
            .FirstOrDefaultAsync(x =>
                x.UserId == userId &&
                x.Layer == layer &&
                x.Key == key);
    }

    // 🔹 GET ALL
    // PERF (MVP freeze): UserTrendService.ComputeInterestAffinity her aday maç için
    // çağrılıyordu → aynı kullanıcı için 100 özdeş sorgu. Repository Scoped olduğundan
    // önbellek İSTEK BAŞINA geçerlidir; içerik aynı, round-trip 100 → 1.
    private readonly Dictionary<int, List<UserInterestScore>> _byUserCache = new();

    public async Task<List<UserInterestScore>> GetByUser(int userId)
    {
        if (_byUserCache.TryGetValue(userId, out var cached))
            return cached;

        var scores = await _context.UserInterestScores
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync();

        _byUserCache[userId] = scores;
        return scores;
    }

    // 🔥 ENTITY UPSERT
    public async Task UpsertAsync(UserInterestScore entity)
    {
        entity.Layer = Normalize(entity.Layer);
        entity.Key = Normalize(entity.Key);

        var existing = await GetAsync(entity.UserId, entity.Layer, entity.Key);

        if (existing == null)
        {
            await _context.Set<UserInterestScore>().AddAsync(entity);
        }
        else
        {
            existing.Score += entity.Score;
            existing.UpdatedAtUtc = DateTime.UtcNow;
            existing.LastEventAtUtc = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        // Yazim sonrasi istek-ici onbellek bayat kalmasin.
        _byUserCache.Clear();
    }

    // 🔥 FULL SET UPSERT
    public async Task UpsertAsync(int userId, string layer, string key, int score, DateTime updatedAtUtc)
    {
        layer = Normalize(layer);
        key = Normalize(key);

        var existing = await GetAsync(userId, layer, key);

        if (existing == null)
        {
            await _context.Set<UserInterestScore>().AddAsync(new UserInterestScore
            {
                UserId = userId,
                Layer = layer,
                Key = key,
                Score = score,
                LastEventAtUtc = updatedAtUtc,
                UpdatedAtUtc = updatedAtUtc
            });
        }
        else
        {
            existing.Score = score;
            existing.UpdatedAtUtc = updatedAtUtc;
        }

        await _context.SaveChangesAsync();

        // Yazim sonrasi istek-ici onbellek bayat kalmasin.
        _byUserCache.Clear();
    }

    // 🔥 DELTA (ANA METHOD)
    public async Task UpsertDeltaAsync(int userId, string layer, string key, int delta, DateTime updatedAtUtc)
    {
        layer = Normalize(layer);
        key = Normalize(key);

        var existing = await GetAsync(userId, layer, key);

        if (existing == null)
        {
            await _context.Set<UserInterestScore>().AddAsync(new UserInterestScore
            {
                UserId = userId,
                Layer = layer,
                Key = key,
                Score = delta,
                LastEventAtUtc = updatedAtUtc,
                UpdatedAtUtc = updatedAtUtc
            });
        }
        else
        {
            existing.Score += delta;
            existing.UpdatedAtUtc = updatedAtUtc;
            existing.LastEventAtUtc = updatedAtUtc;
        }

        await _context.SaveChangesAsync();

        // Yazim sonrasi istek-ici onbellek bayat kalmasin.
        _byUserCache.Clear();
    }
}