using Formax.Application.DTOs.MatchIntelligence;

namespace Formax.Application.Services.MatchIntelligence.Lineup;

/// <summary>
/// Beklenen ↔ resmi 11 oyuncu-bazlı fark analizi (Görev #014). İsimle eşleştirir (kararlı kimlik).
///   in     : resmi'de var, beklenende yok (İlk 11'e girdi)
///   out    : beklenende var, resmi'de yok (İlk 11'den çıktı)
///   moved  : ikisinde de var, pozisyon farklı (Pozisyon değişti)
///   same   : ikisinde de var, pozisyon aynı (Aynı kaldı)
/// </summary>
public static class LineupDifferenceAnalyzer
{
    public sealed record Diff(
        List<LivingPlayerDto> Added,
        List<LivingPlayerDto> Removed,
        List<LivingPlayerDto> Moved,
        List<LivingPlayerDto> Unchanged,
        List<LivingPlayerDto> OfficialWithStatus);

    public static Diff Compare(
        IReadOnlyList<ExpectedPlayerDto> expected,
        IReadOnlyList<ExpectedPlayerDto> official)
    {
        var expByName = new Dictionary<string, ExpectedPlayerDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in expected)
            expByName.TryAdd(e.Name, e);

        var offNames = new HashSet<string>(official.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);

        var added = new List<LivingPlayerDto>();
        var moved = new List<LivingPlayerDto>();
        var unchanged = new List<LivingPlayerDto>();
        var officialStatus = new List<LivingPlayerDto>();

        foreach (var o in official)
        {
            LivingPlayerDto p;
            if (!expByName.TryGetValue(o.Name, out var e))
            {
                p = Make(o, "in", null);
                added.Add(p);
            }
            else if (!string.Equals(e.Position, o.Position, StringComparison.OrdinalIgnoreCase))
            {
                p = Make(o, "moved", e.Position);
                moved.Add(p);
            }
            else
            {
                p = Make(o, "same", null);
                unchanged.Add(p);
            }
            officialStatus.Add(p);
        }

        var removed = expected
            .Where(e => !offNames.Contains(e.Name))
            .Select(e => Make(e, "out", null))
            .ToList();

        return new Diff(added, removed, moved, unchanged, officialStatus);
    }

    private static LivingPlayerDto Make(ExpectedPlayerDto p, string status, string? from) => new()
    {
        Number = p.Number, Name = p.Name, Position = p.Position, Role = p.Role,
        Status = status, FromPosition = from
    };
}
