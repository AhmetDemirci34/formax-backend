using Formax.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class TrackFeedEventRequestDto
{
    public int MatchId { get; set; }

    public string EventType { get; set; } = "";

    public FeedSignalType SignalType { get; set; }

    public double? DwellTimeSeconds { get; set; }

    // 🔥 NEW (CRITICAL)
    public double? ScoreAtServe { get; set; }

    public int? PositionAtServe { get; set; }

    public string DecisionType { get; set; } = "";
}
