using System;
using Formax.Domain.Entities;

namespace Formax.Application.Services.Recommendation;

public class RewardCalculator
{
    // 🔒 ESKİ SİSTEM (KORUNDU)
    public double Calculate(int clicks, double dwell, int follows, int skips)
    {
        return
            (clicks * 1.0) +
            (dwell * 0.5) +
            (follows * 2.0) -
            (skips * 0.2);
    }

    // 🔥 MULTI-SIGNAL MODEL (GÜNCELLENDİ)
    public class MultiSignalReward
    {
        public double ClickReward { get; set; }
        public double SkipPenalty { get; set; }
        public double DwellReward { get; set; }
        public double FollowReward { get; set; }

        // 🔥 YENİ
        public double CtrBoost { get; set; }

        public double Total =>
            ClickReward + DwellReward + FollowReward + CtrBoost - SkipPenalty;
    }

    // 🔥 CTR + SKIP ENTEGRE
    public MultiSignalReward CalculateMulti(
        FeedInteractionEvent e,
        double ctr = 0,
        double skipRate = 0,
        bool hasClick = false,
        bool hasFollow = false)
    {
        var r = new MultiSignalReward();

        // 🔹 EVENT BASE
        switch (e.SignalType)
        {
            case FeedSignalType.Click:
                r.ClickReward = 1.0;
                break;

            case FeedSignalType.Skip:
                r.SkipPenalty = 0.3;
                break;

            case FeedSignalType.Dwell:
                r.DwellReward = Math.Min((e.DwellTimeSeconds ?? 0) / 10.0, 1.5);
                break;

            case FeedSignalType.Follow:
                r.FollowReward = 2.0;
                break;

            case FeedSignalType.Impression:
                break;
        }

        // 🔥 CLICK BOOST (global)
        if (hasClick)
            r.ClickReward += 1.0;

        // 🔥 FOLLOW BOOST
        if (hasFollow)
            r.FollowReward += 0.5;

        // 🔥 CTR BOOST
        if (ctr > 0)
            r.CtrBoost = Math.Min(ctr, 1.0) * 0.5;

        // 🔥 SKIP PENALTY (GLOBAL)
        if (skipRate > 0)
            r.SkipPenalty += skipRate * 0.7;

        return r;
    }

    // 🔥 YENİ: FINAL SCORE (PHASE 7.4 CORE)
    public double CalculateFinalScore(
        double ctr,
        double skipRate,
        bool hasClick,
        bool hasFollow)
    {
        double clickReward = hasClick ? 1.0 : 0.0;
        double followReward = hasFollow ? 0.5 : 0.0;

        double ctrBoost = ctr > 0 ? Math.Min(ctr, 1.0) * 0.5 : 0.0;
        double skipPenalty = skipRate > 0 ? skipRate * -0.7 : 0.0;

        double final =
            clickReward +
            followReward +
            ctrBoost +
            skipPenalty;

        return Math.Clamp(final, -1, 2);
    }

    // 🔥 EVENT LEVEL (KORUNDU ama normalize edildi)
    public double CalculateFromEvent(FeedInteractionEvent e)
    {
        return e.SignalType switch
        {
            FeedSignalType.Impression => 0.0, // ❗ artık nötr

            FeedSignalType.Click => 1.0,

            FeedSignalType.Follow => 2.0,

            FeedSignalType.Skip => -0.3,

            FeedSignalType.Dwell => Math.Min((e.DwellTimeSeconds ?? 0) / 10.0, 1.5),

            _ => 0
        };
    }
}