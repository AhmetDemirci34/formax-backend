using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.AI.Confidence
{
    /// <summary>
    /// Confidence hesaplaması için gereken saf bağlam verileri
    /// </summary>
    public class ConfidenceContext
    {
        // Match
        public bool IsPreMatch { get; init; }
        public bool IsLive { get; init; }
        public bool IsFinished { get; init; }

        // PreMatch signals
        public bool IsLineupAnnounced { get; init; }
        public bool IsWorldPerceptionHigh { get; init; }

        // Live signals
        public int LiveMinute { get; init; }
        public bool HasMajorEvent { get; init; }

        // Memory / decay
        public bool HasRecentExtendedContext { get; init; }
    }
}

