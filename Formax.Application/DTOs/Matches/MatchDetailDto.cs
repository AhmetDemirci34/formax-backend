using System;
using System.Collections.Generic;

namespace Formax.Application.DTOs.Matches;

public class MatchDetailDto
{
    public int MatchId { get; set; }

    public TeamSummaryDto HomeTeam { get; set; } = new();
    public TeamSummaryDto AwayTeam { get; set; } = new();

    public DateTime MatchDate { get; set; }
    public string Status { get; set; } = string.Empty;

    public List<LastMatchDto> HomeTeamLastMatches { get; set; } = new();
    public List<LastMatchDto> AwayTeamLastMatches { get; set; } = new();

    public SapmaDto Sapma { get; set; } = new();
    public AiDto Ai { get; set; } = new();
    public UserProtectionDto UserProtection { get; set; } = new();
}

// ---------------- SAPMA ----------------

public class SapmaDto
{
    public int OynanmaSkoru { get; set; }
    public int GucSkoru { get; set; }
    public int Sapma { get; set; }

    public string OynanmaYonu { get; set; } = "";
    public string GercekGucYonu { get; set; } = "";

    public string SapmaBolgesi { get; set; } = "";
    public bool SessizMi { get; set; }

    public string SapmaMetni { get; set; } = "";
}

// ---------------- AI ----------------

public class AiDto
{
    public string State { get; set; } = "";
    public string Summary { get; set; } = "";
}

// ---------------- USER PROTECTION ----------------

public class UserProtectionDto
{
    public string ResponsibilityNote { get; set; } = "";
    public bool DecisionIsYours { get; set; }
}