using Formax.Application.DTOs.Live;
using Formax.Application.DTOs.MatchIntelligence;

namespace Formax.Application.Services.MatchIntelligence.Live;

/// <summary>Maç ritmi: dakika başına tehlikeli atak + şut yoğunluğundan (Görev #019).</summary>
public static class LiveRhythmAnalyzer
{
    public static string Rhythm(LiveStatsDto s)
    {
        var minute = Math.Max(s.Minute ?? 1, 1);
        var dangerous = s.DangerousAttacksHome + s.DangerousAttacksAway;
        var shots = s.ShotsHome + s.ShotsAway;
        var perMin = (dangerous + shots * 2.0) / minute;
        return perMin >= 2.0 ? "Yüksek" : perMin >= 1.0 ? "Orta" : "Düşük";
    }
}

/// <summary>Momentum: momentum snapshot varsa ondan, yoksa canlı stats'tan türetilir (uydurma yok).</summary>
public static class LiveMomentumAnalyzer
{
    public static (string Side, int Value) Momentum(IReadOnlyList<MomentumSnapshotDto> snapshots, LiveStatsDto stats)
    {
        double home, away;
        if (snapshots.Count > 0)
        {
            var recent = snapshots.Skip(Math.Max(0, snapshots.Count - 5)).ToList();
            home = recent.Sum(m => (double)m.HomePressure);
            away = recent.Sum(m => (double)m.AwayPressure);
        }
        else
        {
            // Momentum snapshot yok → canlı istatistikten türet (tehlikeli atak + şut + topa sahip olma).
            home = stats.DangerousAttacksHome + stats.ShotsHome * 2 + stats.PossessionHome * 0.3;
            away = stats.DangerousAttacksAway + stats.ShotsAway * 2 + stats.PossessionAway * 0.3;
        }

        var total = home + away;
        var value = total > 0 ? (int)Math.Round(away / total * 100) : 50; // 0=ev, 100=deplasman
        var side = value < 45 ? "home" : value > 55 ? "away" : "balanced";
        return (side, value);
    }
}

/// <summary>En önemli canlı olay: timeline'da en yüksek ImpactScore (yoksa dürüst boş).</summary>
public static class LiveEventAnalyzer
{
    public static LiveKeyEventDto KeyEvent(IReadOnlyList<LiveEventDto> timeline, string homeName, string awayName)
    {
        if (timeline.Count == 0)
            return new LiveKeyEventDto { Type = "—", Description = "Kayıtlı önemli olay yok", HasEvent = false };

        var top = timeline.OrderByDescending(e => e.ImpactScore).First();
        var team = string.Equals(top.TeamName, homeName, StringComparison.OrdinalIgnoreCase) ? "home" : "away";
        var desc = !string.IsNullOrWhiteSpace(top.Detail) ? top.Detail : top.PlayerName;

        return new LiveKeyEventDto
        {
            Minute = $"{top.Minute}'",
            Type = top.EventType,
            Description = string.IsNullOrWhiteSpace(desc) ? top.EventType : desc,
            Team = team,
            HasEvent = true,
        };
    }
}
