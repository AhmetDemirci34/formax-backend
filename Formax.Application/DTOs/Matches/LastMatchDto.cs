namespace Formax.Application.DTOs.Matches;

public class LastMatchDto
{
    public int? MatchId { get; set; }
    public string Opponent { get; set; } = string.Empty;

    /// <summary>"W" | "D" | "L"</summary>
    public string Result { get; set; } = string.Empty;

    /// <summary>Score from the perspective of the team, e.g. "3-0".</summary>
    public string Score { get; set; } = string.Empty;

    /// <summary>
    /// İlk yarı skoru — MAÇIN kendi yönünde (ev sahibi - deplasman), takım perspektifinde
    /// DEĞİL. Sağlayıcı vermediyse null; UI o zaman "—" gösterir, tahmin ETMEZ.
    /// </summary>
    public int? HalfTimeHomeScore { get; set; }
    public int? HalfTimeAwayScore { get; set; }

    /// <summary>Display date string, e.g. "07.01.2024".</summary>
    public string Date { get; set; } = string.Empty;

    public string Competition { get; set; } = string.Empty;

    public bool IsHome { get; set; }
}
