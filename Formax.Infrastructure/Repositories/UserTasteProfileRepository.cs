using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;


public class UserTasteProfileRepository : IUserTasteProfileRepository
{
    private readonly FormaxDbContext _context;

    public UserTasteProfileRepository(FormaxDbContext context)
    {
        _context = context;
    }

    public async Task<UserTasteProfile?> GetByUserId(int userId)
    {
        return await _context.Set<UserTasteProfile>()
            .FirstOrDefaultAsync(x => x.UserId == userId);
    }

    public async Task Save(UserTasteProfile profile)
    {
        if (profile.Id == 0)
            await _context.AddAsync(profile);
        else
            _context.Update(profile);

        await _context.SaveChangesAsync();
    }
}