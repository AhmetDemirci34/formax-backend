using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.AI.Contexts
{
    /// <summary>
    /// Dış dünya algısının
    /// AI için sade özeti.
    /// </summary>
    public sealed class WorldPerceptionContext
    {
        public string BatchSnapshotId { get; init; } = null!;
        public bool HasIntradayUpdate { get; init; }
        public GlobalAttentionLevel GlobalAttentionLevel { get; init; }
    }

    public enum GlobalAttentionLevel
    {
        Low,
        Medium,
        High
    }
}

