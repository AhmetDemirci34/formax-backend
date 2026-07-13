using Formax.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.Interfaces
{
    public interface IUserPreferenceRepository
    {
        Task<UserPreferenceWeights> GetOrCreate(int userId);
        Task Update(UserPreferenceWeights w);
    }
}
