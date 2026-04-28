using Formax.Domain.Entities;
using Microsoft.EntityFrameworkCore; // 🔥 ZORUNLU

namespace Formax.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<User> Users { get; set; }
    DbSet<UserStats> UserStats { get; set; }
    DbSet<MatchTrendStat> MatchTrendStats { get; set; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    DbSet<UserAction> UserActions { get; }
}