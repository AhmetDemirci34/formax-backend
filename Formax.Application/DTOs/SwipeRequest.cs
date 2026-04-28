using System.Text.Json.Serialization;

public class SwipeRequest
{
    [JsonPropertyName("matchId")]
    public int MatchId { get; set; }

    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("confidenceLabel")]
    public string ConfidenceLabel { get; set; } = string.Empty;

    [JsonPropertyName("isTrending")]
    public bool IsTrending { get; set; }

    [JsonPropertyName("oddsDrop")]
    public int OddsDrop { get; set; }

    [JsonPropertyName("odds")]
    public double Odds { get; set; }

    [JsonPropertyName("viewDurationMs")]
    public int ViewDurationMs { get; set; }

    // 🔥 EKLENDİ
    [JsonPropertyName("team")]
    public string Team { get; set; } = string.Empty;
}