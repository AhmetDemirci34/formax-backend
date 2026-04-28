using System;

namespace Formax.Domain.Entities;

public class UserStats
{
    public int UserId { get; set; } 

    public int Total { get; set; }
    public int Win { get; set; }
    public int Lose { get; set; }

    public int CurrentStreak { get; set; }
    public int BestStreak { get; set; }

    public int Level { get; set; } = 1;

    // 🔥 YENİ (LEARNING)
    public int PlayCount { get; set; } = 0;
    public int PassCount { get; set; } = 0;
    public int SafePreference { get; set; } = 0;
    public int RiskPreference { get; set; } = 0;
    public int ViewCount { get; set; }

    public double Accuracy =>
        Total == 0 ? 0 : (double)Win / Total * 100;
}