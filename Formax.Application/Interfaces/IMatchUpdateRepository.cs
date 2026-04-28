using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.Interfaces
{
    public interface IMatchUpdateRepository
    {
        void UpdateLiveStatus(int matchId, string status, string? matchMinute);
    }
}
