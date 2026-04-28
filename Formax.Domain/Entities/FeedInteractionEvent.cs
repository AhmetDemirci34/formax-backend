using System;

namespace Formax.Domain.Entities;

public enum FeedSignalType
{
    Impression = 0, // 🔥 EKLENDİ

    Click = 1,
    Skip = 2,
    Dwell = 3,
    Follow = 4
}

public class FeedInteractionEvent
{
    public int Id { get; set; }

    public int MatchId { get; set; }

    public int UserId { get; set; }

    // 🔥 eski sistem (geriye uyumluluk için bırak)
    public string EventType { get; set; } = "";

    // 🔥 YENİ: gerçek learning sinyali
    public FeedSignalType SignalType { get; set; }

    // 🔥 dwell learning için
    public double? DwellTimeSeconds { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // 🔥 toplam reward (aggregate)
    public double Reward { get; set; }

    // 🔥 LOGGING BRIDGE
    public double? ScoreAtServe { get; set; }

    public int? PositionAtServe { get; set; }

    public string DecisionType { get; set; } = "";
}