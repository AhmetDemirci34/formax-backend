namespace Formax.Domain.Entities
{
    /// <summary>
    /// Momentum sample recorded every 30 seconds during a live match.
    /// HomePressure and AwayPressure are 0-100 and sum to 100.
    /// Derived from dangerous attacks ratio with possession as fallback.
    /// </summary>
    public class MatchMomentumSnapshot
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public int MatchId { get; set; }

        /// <summary>Match minute bucket when this snapshot was taken.</summary>
        public int MinuteBucket { get; set; }

        /// <summary>Home team pressure index (0-100).</summary>
        public int HomePressure { get; set; }

        /// <summary>Away team pressure index (0-100).</summary>
        public int AwayPressure { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
