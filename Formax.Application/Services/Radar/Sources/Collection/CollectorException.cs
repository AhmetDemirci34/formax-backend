using System;

namespace Formax.Application.Services.Radar.Sources.Collection
{
    /// <summary>
    /// Radar Source Engine (R.8.3) — internal error raised by a collector's core
    /// logic. Caught by <see cref="SourceCollectorBase"/> and converted into a failed
    /// <see cref="SourceCollectorResult"/>; it never escapes to the dispatcher.
    /// </summary>
    public sealed class CollectorException : Exception
    {
        public string SourceKey { get; }

        public CollectorException(string sourceKey, string message)
            : base(message)
        {
            SourceKey = sourceKey;
        }

        public CollectorException(string sourceKey, string message, Exception inner)
            : base(message, inner)
        {
            SourceKey = sourceKey;
        }
    }
}
