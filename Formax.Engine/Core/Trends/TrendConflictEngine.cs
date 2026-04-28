using System;

namespace Formax.Engine.Core.Trends;

public class TrendConflictEngine
{
    public TrendConflictResult Analyze(double userTrend, double marketTrend)
    {
        userTrend = Math.Clamp(userTrend, 0, 1);
        marketTrend = Math.Clamp(marketTrend, 0, 1);

        var diff = Math.Abs(userTrend - marketTrend);

        // =========================
        // 1) ALIGNMENT (PRIMARY)
        // =========================
        if (diff < 0.2)
        {
            if (userTrend > 0.7 && marketTrend > 0.7)
            {
                return new TrendConflictResult
                {
                    Label = "HOT",
                    Reason = "Strong alignment",
                    ScoreModifier = +10,
                    Severity = diff
                };
            }

            return new TrendConflictResult
            {
                Label = "GOOD",
                Reason = "Aligned",
                ScoreModifier = +3,
                Severity = diff
            };
        }

        // =========================
        // 2) EXTREME CONFLICT ONLY
        // =========================
        if (userTrend < 0.2 && marketTrend > 0.85)
        {
            return new TrendConflictResult
            {
                Label = "RISKY",
                Reason = "Market high / user none",
                ScoreModifier = -15,
                Severity = diff
            };
        }

        if (marketTrend < 0.2 && userTrend > 0.85)
        {
            return new TrendConflictResult
            {
                Label = "VALUE",
                Reason = "User strong / market blind",
                ScoreModifier = +20,
                Severity = diff
            };
        }

        // =========================
        // 3) MID → GOOD
        // =========================
        if (userTrend > 0.5 && marketTrend > 0.5)
        {
            return new TrendConflictResult
            {
                Label = "GOOD",
                Reason = "Balanced",
                ScoreModifier = +2,
                Severity = diff
            };
        }

        // =========================
        // 4) LOW SIGNAL
        // =========================
        if (userTrend < 0.25 && marketTrend < 0.4)
        {
            return new TrendConflictResult
            {
                Label = "BAD",
                Reason = "Low signal",
                ScoreModifier = -10,
                Severity = diff
            };
        }

        // =========================
        // DEFAULT → GOOD (ASLA RISKY DEĞİL)
        // =========================
        return new TrendConflictResult
        {
            Label = "GOOD",
            Reason = "Neutral",
            ScoreModifier = 0,
            Severity = diff
        };
    }
}

public class TrendConflictResult
{
    public string Label { get; set; } = "";
    public string Reason { get; set; } = "";
    public int ScoreModifier { get; set; }
    public double Severity { get; set; }
}