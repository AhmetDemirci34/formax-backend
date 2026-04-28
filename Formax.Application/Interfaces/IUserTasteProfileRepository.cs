using Formax.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.Interfaces
{
    public interface IUserTasteProfileRepository
    {
        Task<UserTasteProfile?> GetByUserId(int userId);
        Task Save(UserTasteProfile profile);
    }
}
