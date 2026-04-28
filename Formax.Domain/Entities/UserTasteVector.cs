using System;
using System.Collections.Generic;

namespace Formax.Domain.Entities;

public class UserTasteVector
{
    public int UserId { get; set; }

    // 🔥 MATCH BASED (ŞU AN AKTİF)
    public Dictionary<int, double> MatchAffinity { get; set; } = new();

    // 🔒 GELECEK (DTO GELİNCE AKTİF EDİLECEK)
    public Dictionary<int, double> TeamAffinity { get; set; } = new();
    public Dictionary<string, double> LeagueAffinity { get; set; } = new();

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}