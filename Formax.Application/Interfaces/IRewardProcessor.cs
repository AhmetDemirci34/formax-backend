using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.Interfaces;

public interface IRewardProcessor
{
    Task ProcessEvent(int matchId, string eventType, int dwellTime);
}
