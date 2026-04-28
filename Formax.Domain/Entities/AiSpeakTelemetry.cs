using Formax.Domain.Subscriptions;

using System;

namespace Formax.Domain.Entities
{
    public class AiSpeakTelemetry
    {
        public int Id { get; set; }

        public Guid? UserId { get; set; }
        public int MatchId { get; set; }

        public bool CanSpeak { get; set; }

        /// <summary>
        /// AI konuşmadıysa nedeni (Cooldown, NoContext, Repetition vb.)
        /// </summary>
        public string? SilenceReason { get; set; }

        /// <summary>
        /// Free / Premium / Intro gibi erişim seviyesi
        /// </summary>
        public AccessLevel AccessLevel { get; set; }

        public DateTime CreatedAtUtc { get; set; }
    }
}