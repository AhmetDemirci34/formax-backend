using System;

public class UserAction
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int MatchId { get; set; }

    public int ActionType { get; set; } = 0; // 🔥 FIX (int)

    public double Odds { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int ViewDurationMs { get; set; } = 0;
    public bool OpenedDetail { get; set; } = false;
    public bool Followed { get; set; } = false;
    public string? Team { get; set; }
}