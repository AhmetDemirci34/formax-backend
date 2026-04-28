using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.Common.Enums;

public enum FeedEventType
{
    Impression = 1,
    Click = 2,
    Dwell = 3,
    Skip = 4,
    Follow = 5,
    FirstSessionStarted,
    FirstMatchViewed,
    First3SwipesCompleted,
    FirstMatchOpened,
    FirstSessionEnded,
}
