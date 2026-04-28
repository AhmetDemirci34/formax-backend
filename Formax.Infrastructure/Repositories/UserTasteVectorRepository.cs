using Formax.Application.Interfaces;
using Formax.Domain.Entities;

namespace Formax.Infrastructure.Repositories;

public class UserTasteVectorRepository : IUserTasteVectorRepository
{
    private static readonly Dictionary<int, UserTasteVector> _store = new();

    public Task<UserTasteVector?> Get(int userId)
    {
        _store.TryGetValue(userId, out var value);
        return Task.FromResult(value);
    }

    public Task Save(UserTasteVector vector)
    {
        _store[vector.UserId] = vector;
        return Task.CompletedTask;
    }
}