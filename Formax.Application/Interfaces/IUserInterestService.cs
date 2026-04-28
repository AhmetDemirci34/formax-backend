using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public interface IUserInterestService
{
    Task<UserInterestProfile> GetProfileAsync(int userId, DateTime utcNow);
}