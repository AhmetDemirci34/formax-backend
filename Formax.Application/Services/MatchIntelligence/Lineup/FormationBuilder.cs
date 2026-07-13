namespace Formax.Application.Services.MatchIntelligence.Lineup;

/// <summary>
/// Diziliş çıkarımı. Domain pozisyonları kaba (G/D/M/F) olduğundan, diziliş savunma/orta/hücum
/// sayımından frontend'in desteklediği sete (4-3-3 · 4-2-3-1 · 3-5-2 · 4-4-2) eşlenir.
/// </summary>
public static class FormationBuilder
{
    /// <summary>Desteklenen dizilişlerin (D, M, F) sayımları.</summary>
    private static readonly (string Name, int D, int M, int F)[] Supported =
    {
        ("4-3-3",   4, 3, 3),
        ("4-2-3-1", 4, 5, 1),
        ("3-5-2",   3, 5, 2),
        ("4-4-2",   4, 4, 2),
    };

    /// <summary>Verilen D/M/F sayımına EN YAKIN desteklenen dizilişi seçer.</summary>
    public static string Infer(int defenders, int midfielders, int forwards)
    {
        var best = "4-2-3-1";
        var bestDist = int.MaxValue;
        foreach (var f in Supported)
        {
            var dist = Math.Abs(f.D - defenders) + Math.Abs(f.M - midfielders) + Math.Abs(f.F - forwards);
            if (dist < bestDist) { bestDist = dist; best = f.Name; }
        }
        return best;
    }

    /// <summary>Diziliş adının hedef (D, M, F) sayımı.</summary>
    public static (int D, int M, int F) TargetCounts(string formation)
    {
        foreach (var f in Supported)
            if (f.Name == formation) return (f.D, f.M, f.F);
        return (4, 5, 1); // güvenli varsayılan: 4-2-3-1
    }

    public static string RoleLabel(string position) => position switch
    {
        "G" => "Kaleci",
        "D" => "Defans",
        "M" => "Orta Saha",
        "F" => "Forvet",
        _   => "Oyuncu",
    };
}
