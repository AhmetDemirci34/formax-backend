using Formax.Application.DTOs.MatchIntelligence;

namespace Formax.Application.Services.MatchIntelligence.Lineup;

/// <summary>
/// Takım bazlı karşılaştırma analizi: değişiklik sayısı, etkilenen hatlar, formasyon değişimi,
/// AI narrative ve evidence üretir (Görev #014).
/// </summary>
public static class LineupComparisonService
{
    public sealed record Analysis(
        int ChangeCount,
        List<string> AffectedLines,
        bool FormationChanged,
        string AiSummary,
        List<LivingEvidenceDto> Evidence);

    public static Analysis Analyze(
        LineupDifferenceAnalyzer.Diff diff,
        string expectedFormation,
        string officialFormation,
        string teamName)
    {
        var changeCount = diff.Added.Count + diff.Removed.Count + diff.Moved.Count;
        var formationChanged =
            !string.IsNullOrEmpty(expectedFormation) &&
            !string.Equals(expectedFormation, officialFormation, StringComparison.OrdinalIgnoreCase);

        var lines = new HashSet<string>();
        void AddLine(string pos) { var l = LineName(pos); if (l != null) lines.Add(l); }
        foreach (var p in diff.Added) AddLine(p.Position);
        foreach (var p in diff.Removed) AddLine(p.Position);
        foreach (var p in diff.Moved) { AddLine(p.Position); if (p.FromPosition != null) AddLine(p.FromPosition); }

        var summary = BuildSummary(changeCount, formationChanged, expectedFormation, officialFormation, lines, teamName);

        var evidence = new List<LivingEvidenceDto>
        {
            new() { Label = "Giren oyuncular",       Detail = Names(diff.Added) },
            new() { Label = "Çıkan oyuncular",       Detail = Names(diff.Removed) },
            new() { Label = "Pozisyon değişikliği",  Detail = Names(diff.Moved) },
            new() { Label = "Formasyon değişikliği",
                    Detail = formationChanged ? $"{expectedFormation} → {officialFormation}" : "yok" },
        };

        return new Analysis(changeCount, lines.ToList(), formationChanged, summary, evidence);
    }

    private static string BuildSummary(
        int changeCount, bool formationChanged,
        string expectedFormation, string officialFormation,
        HashSet<string> lines, string teamName)
    {
        if (changeCount == 0 && !formationChanged)
            return $"{teamName}: Beklenen ilk 11 birebir korundu.";

        if (formationChanged && changeCount == 0)
            return $"{teamName}: Oyuncular korundu ancak diziliş {expectedFormation} yerine {officialFormation} oldu.";

        if (formationChanged)
            return $"{teamName}: Teknik direktör dizilişi {expectedFormation}→{officialFormation} değiştirdi ({changeCount} oyuncu farkı).";

        if (changeCount <= 2)
            return $"{teamName}: Beklenen kadro büyük ölçüde korundu; {changeCount} değişiklik var.";

        var line = lines.FirstOrDefault(l => l != "Kale");
        return line != null
            ? $"{teamName}: Teknik direktör {line} hattında değişikliğe gitti ({changeCount} değişiklik)."
            : $"{teamName}: Beklenene göre {changeCount} değişiklik var.";
    }

    private static string Names(IReadOnlyList<LivingPlayerDto> players)
        => players.Count == 0 ? "yok" : string.Join(", ", players.Select(p => p.Name));

    private static string? LineName(string pos) => pos switch
    {
        "D" => "Savunma", "M" => "Orta Saha", "F" => "Hücum", "G" => "Kale", _ => null
    };
}
