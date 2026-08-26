namespace Formax.Application.DTOs.Players
{
    /// <summary>
    /// api-football /players?team=&season= tek oyuncu sezon özeti (birden çok stat satırı toplanmış).
    /// Football Intelligence ingestion tarafından okunur. Coverage yoksa liste boş döner.
    /// </summary>
    public sealed class SportsPlayerSeasonStat
    {
        public int PlayerId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Position { get; init; } = string.Empty; // Goalkeeper|Defender|Midfielder|Attacker
        public int Goals { get; init; }
        public int Assists { get; init; }
        public int Minutes { get; init; }
        public int Appearances { get; init; }
        public double? Rating { get; init; }
        public int Yellow { get; init; }
        public int Red { get; init; }
        // v2 — şut/kilit-pas (api-football statistics.shots.total / passes.key)
        public int Shots { get; init; }
        public int KeyPasses { get; init; }
    }

    /// <summary>api-football /injuries?team=&season= tek sakatlık/ceza kaydı.</summary>
    public sealed class SportsTeamInjury
    {
        public int PlayerId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Type { get; init; } = string.Empty;   // ör. "Missing Fixture"
        public string Reason { get; init; } = string.Empty; // ör. "Knee Injury", "Suspended"
    }
}
