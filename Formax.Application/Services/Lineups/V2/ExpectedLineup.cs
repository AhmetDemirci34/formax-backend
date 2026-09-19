using System;
using System.Collections.Generic;
using System.Linq;

namespace Formax.Application.Services.Lineups.V2
{
    /// <summary>
    /// Bir oyuncunun MAÇTAN ÖNCE bilinen kullanım profili — yalnız daha erken başlamış maçlardan.
    /// </summary>
    /// <param name="StartShare">Takımın gözlenen maçlarında ilk 11'de başlama payı (0..1).</param>
    /// <param name="MinuteShare">Gerçek dakika verisi olan maçlarda sahada geçirdiği sürenin payı; veri yoksa null.</param>
    /// <param name="WeightedStartShare">Zaman ağırlıklı başlama payı (yeni maç daha ağır).</param>
    /// <param name="RecentStarts5">Son 5 takım maçında başlangıç sayısı.</param>
    /// <param name="RecentStarts10">Son 10 takım maçında başlangıç sayısı.</param>
    /// <param name="Observations">Bu oyuncunun görüldüğü takım-maç sayısı (kadroda ya da yedekte).</param>
    /// <param name="Starts">İlk 11 başlangıç sayısı.</param>
    /// <param name="Position">Baskın mevki (G/D/M/F); bilinmiyorsa null.</param>
    public sealed record PlayerUsage(
        double StartShare,
        double? MinuteShare,
        double WeightedStartShare,
        int RecentStarts5,
        int RecentStarts10,
        int Observations,
        int Starts,
        string? Position)
    {
        public static readonly PlayerUsage Unknown = new(0, null, 0, 0, 0, 0, 0, null);

        /// <summary>Kullanım profili gerçekten öğrenilmiş mi? (bilinmeyen oyuncuya uydurma değer verilmez)</summary>
        public bool Known => Observations > 0;
    }

    /// <summary>
    /// BEKLENEN İLK 11 — yalnız maçtan ÖNCE oynanmış maçlardan kurulur.
    ///
    /// Kural: takımın gözlenen kadrolarında zaman ağırlıklı başlama payı en yüksek 11 oyuncu.
    /// Ağırlık, maçın eskiliğiyle üstel azalır (<see cref="ExpectedLineupBuilder.HalfLifeMatches"/>).
    /// Mevki kotası ZORLANMAZ: kaynak mevkileri eksik olabilir ve kota, kadro değişimini
    /// yapay olarak bastırırdı. Beklenen 11 kurulamazsa (yetersiz geçmiş) null döner ve
    /// kadro etkisi UYGULANMAZ.
    /// </summary>
    public sealed record ExpectedLineup(
        int TeamId,
        DateTime CutoffUtc,
        IReadOnlyList<string> ExpectedStarters,
        IReadOnlyDictionary<string, PlayerUsage> Usage,
        int ObservedTeamMatches)
    {
        public PlayerUsage UsageOf(string playerKey) => Usage.GetValueOrDefault(playerKey, PlayerUsage.Unknown);
    }

    /// <summary>
    /// Takımın geçmiş kadro gözlemlerinden beklenen ilk 11'i ve oyuncu kullanım profillerini üretir.
    /// SAF: ağ ve DB yok. Tek girdi, kesim anından ÖNCE başlamış maçların kadrolarıdır.
    /// </summary>
    public static class ExpectedLineupBuilder
    {
        /// <summary>Zaman ağırlığının yarılandığı maç sayısı (yeni maç daha ağır).</summary>
        public const double HalfLifeMatches = 8.0;

        /// <summary>Beklenen ilk 11'in kurulabilmesi için gereken en az takım-maç gözlemi.</summary>
        public const int MinTeamMatches = 5;

        /// <summary>Tek takım-maç gözlemi (kadro + gerçek dakika, varsa).</summary>
        public sealed record TeamMatchLineup(
            int MatchId,
            DateTime KickoffUtc,
            int TeamId,
            IReadOnlyList<LineupSlot> Slots);

        /// <param name="MinutesPlayed">Kaynak gerçekten yayımladıysa sahada geçirilen dakika; yoksa null (90 VARSAYILMAZ).</param>
        public sealed record LineupSlot(string PlayerKey, string? Position, bool Starter, int? MinutesPlayed);

        /// <summary>
        /// Kesimden önceki takım maçlarından beklenen 11 ve kullanım profilleri.
        /// <paramref name="history"/> AYNI takıma ait olmalı ve kesimden önce başlamış maçları içermelidir.
        /// </summary>
        public static ExpectedLineup? Build(int teamId, DateTime cutoffUtc, IReadOnlyList<TeamMatchLineup> history)
        {
            var past = history.Where(h => h.TeamId == teamId && h.KickoffUtc < cutoffUtc)
                              .OrderByDescending(h => h.KickoffUtc)
                              .ToList();
            if (past.Count < MinTeamMatches) return null;

            var acc = new Dictionary<string, Accumulator>(StringComparer.Ordinal);
            var last5 = past.Take(5).Select(p => p.MatchId).ToHashSet();
            var last10 = past.Take(10).Select(p => p.MatchId).ToHashSet();

            for (var i = 0; i < past.Count; i++)
            {
                // i = 0 en yeni maç. Ağırlık 0,5^(i / yarıÖmür).
                var weight = Math.Pow(0.5, i / HalfLifeMatches);
                foreach (var slot in past[i].Slots)
                {
                    if (string.IsNullOrEmpty(slot.PlayerKey)) continue;
                    if (!acc.TryGetValue(slot.PlayerKey, out var a)) acc[slot.PlayerKey] = a = new Accumulator();
                    a.Observations++;
                    a.Weight += weight;
                    if (slot.Starter)
                    {
                        a.Starts++;
                        a.WeightedStarts += weight;
                        if (last5.Contains(past[i].MatchId)) a.RecentStarts5++;
                        if (last10.Contains(past[i].MatchId)) a.RecentStarts10++;
                    }
                    if (slot.MinutesPlayed is { } m) { a.Minutes += m; a.MinuteMatches++; }
                    if (slot.Position is { Length: > 0 } pos)
                        a.PositionCounts[pos] = a.PositionCounts.GetValueOrDefault(pos) + 1;
                }
            }

            var totalWeight = Enumerable.Range(0, past.Count).Sum(i => Math.Pow(0.5, i / HalfLifeMatches));
            var usage = acc.ToDictionary(
                kv => kv.Key,
                kv => new PlayerUsage(
                    StartShare: kv.Value.Starts / (double)past.Count,
                    // Dakika payı YALNIZ gerçek dakika gözlemi olan maçlardan; hiç yoksa null.
                    MinuteShare: kv.Value.MinuteMatches == 0 ? null : kv.Value.Minutes / (90.0 * kv.Value.MinuteMatches),
                    WeightedStartShare: totalWeight <= 0 ? 0 : kv.Value.WeightedStarts / totalWeight,
                    RecentStarts5: kv.Value.RecentStarts5,
                    RecentStarts10: kv.Value.RecentStarts10,
                    Observations: kv.Value.Observations,
                    Starts: kv.Value.Starts,
                    Position: kv.Value.PositionCounts.Count == 0
                        ? null
                        : kv.Value.PositionCounts.OrderByDescending(p => p.Value).ThenBy(p => p.Key).First().Key),
                StringComparer.Ordinal);

            var expected = usage
                .OrderByDescending(kv => kv.Value.WeightedStartShare)
                .ThenByDescending(kv => kv.Value.Starts)
                .ThenBy(kv => kv.Key, StringComparer.Ordinal)
                .Take(11)
                .Select(kv => kv.Key)
                .ToList();

            return expected.Count < 11 ? null : new ExpectedLineup(teamId, cutoffUtc, expected, usage, past.Count);
        }

        private sealed class Accumulator
        {
            public int Observations, Starts, RecentStarts5, RecentStarts10, Minutes, MinuteMatches;
            public double Weight, WeightedStarts;
            public Dictionary<string, int> PositionCounts { get; } = new(StringComparer.Ordinal);
        }
    }
}
