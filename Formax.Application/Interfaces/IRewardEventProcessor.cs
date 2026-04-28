using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public interface IRewardEventProcessor
{
    Task ProcessEvent(int matchId, string eventType, int dwellSeconds = 0);
}
