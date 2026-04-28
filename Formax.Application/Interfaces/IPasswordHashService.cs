using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.Interfaces
{
    public interface IPasswordHashService
    {
        string Hash(string password);
        bool Verify(string hash, string password);
    }
}
