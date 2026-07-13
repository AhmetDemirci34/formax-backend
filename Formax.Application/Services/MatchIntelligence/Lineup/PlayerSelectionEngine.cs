using Formax.Domain.Entities;

namespace Formax.Application.Services.MatchIntelligence.Lineup;

/// <summary>
/// Muhtemel 11'i seçen saf motor. Son maçların başlayan 11'lerini recency-ağırlıklı toplar,
/// kadro dışı oyuncuları eler, hedef dizilişe (D/M/F) göre en yüksek başlama sıklıklı oyuncuları seçer.
/// Uydurma yok — yalnız gerçekte başlamış oyuncular aday.
/// </summary>
public static class PlayerSelectionEngine
{
    public sealed record Selected(int Number, string Name, string Position, int Confidence);

    /// <param name="snapshots">Son maçların başlayan 11'leri; index 0 = en yeni.</param>
    public static List<Selected> Select(
        IReadOnlyList<IReadOnlyList<MatchLineupPlayer>> snapshots,
        ISet<string> unavailable,
        (int D, int M, int F) target)
    {
        var n = snapshots.Count;
        if (n == 0) return new List<Selected>();

        // İsim → (ağırlık, en-yeni numara, en-yeni pozisyon)
        var agg = new Dictionary<string, (double weight, int number, string pos)>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < n; i++)
        {
            var w = n - i; // recency ağırlığı
            foreach (var p in snapshots[i])
            {
                if (string.IsNullOrWhiteSpace(p.PlayerName)) continue;
                if (!agg.TryGetValue(p.PlayerName, out var cur))
                    cur = (0, p.ShirtNumber, p.Position); // ilk görülen = en yeni
                cur.weight += w;
                agg[p.PlayerName] = cur;
            }
        }

        var maxWeight = n * (n + 1) / 2.0; // her maç başlamış olsaydı
        if (maxWeight <= 0) maxWeight = 1;

        var pool = agg
            .Where(kv => !unavailable.Contains(kv.Key))
            .Select(kv => new Cand(kv.Key, kv.Value.number, Bucket(kv.Value.pos),
                (int)Math.Round(Math.Min(100, kv.Value.weight / maxWeight * 100)), kv.Value.weight))
            .OrderByDescending(c => c.Weight)
            .ToList();

        var picked = new List<Selected>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Take(string bucket, int count)
        {
            foreach (var c in pool.Where(c => c.Pos == bucket && !used.Contains(c.Name)).Take(count))
            {
                picked.Add(new Selected(c.Number, c.Name, c.Pos, c.Confidence));
                used.Add(c.Name);
            }
        }

        Take("G", 1);
        Take("D", target.D);
        Take("M", target.M);
        Take("F", target.F);

        // Hat eksik kaldıysa (yeterli aday yok) en yüksek ağırlıklı kalanlarla 11'e tamamla.
        foreach (var c in pool.Where(c => !used.Contains(c.Name)))
        {
            if (picked.Count >= 11) break;
            picked.Add(new Selected(c.Number, c.Name, c.Pos, c.Confidence));
            used.Add(c.Name);
        }

        return picked;
    }

    private static string Bucket(string pos) => pos switch
    {
        "G" => "G", "D" => "D", "M" => "M", "F" => "F", _ => "M",
    };

    private sealed record Cand(string Name, int Number, string Pos, int Confidence, double Weight);
}
