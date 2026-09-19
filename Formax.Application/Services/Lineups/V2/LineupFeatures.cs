using System;
using System.Collections.Generic;
using System.Linq;

namespace Formax.Application.Services.Lineups.V2
{
    /// <summary>
    /// TEK TARAFIN MAÇ ÖNCESİ KADRO ÖZELLİKLERİ — açıklanan ilk 11 ile BEKLENEN ilk 11'in farkı.
    ///
    /// Hepsi yalnız maçtan önce bilinen bilgiden üretilir. Maçın kendi sonucu, olayları ya da
    /// maç sonrası istatistiği HİÇBİRİNE girmez.
    /// </summary>
    public sealed record LineupSideFeatures
    {
        /// <summary>Açıklanan ilk 11'in beklenen 11 ile kesişim oranı (0..1).</summary>
        public double StartingXiContinuity { get; init; }
        /// <summary>Jaccard benzerliği: |kesişim| / |birleşim|.</summary>
        public double StarterJaccard { get; init; }
        /// <summary>Beklenen 11'de olup başlamayan oyuncu sayısı.</summary>
        public int MissingExpectedStarters { get; init; }
        /// <summary>Beklenen 11'de olmayıp başlayan oyuncu sayısı.</summary>
        public int AddedUnexpectedStarters { get; init; }
        /// <summary>Beklenen kaleci başlamadı mı?</summary>
        public bool GoalkeeperChanged { get; init; }
        public int DefensiveUnitChanges { get; init; }
        public int MidfieldUnitChanges { get; init; }
        public int AttackingUnitChanges { get; init; }
        /// <summary>Yüksek kullanım payına sahip olup SON maçta başlamayan, bu maçta başlayan oyuncular.</summary>
        public int ReturningRegulars { get; init; }

        /// <summary>Başlayan oyuncuların ortalama geçmiş başlangıç payı.</summary>
        public double MeanStarterStartShare { get; init; }
        /// <summary>Başlayan oyuncuların ortalama geçmiş dakika payı; hiç dakika verisi yoksa null.</summary>
        public double? MeanStarterMinuteShare { get; init; }
        /// <summary>Başlayanların zaman ağırlıklı kullanım payı ortalaması.</summary>
        public double WeightedStarterShare { get; init; }

        /// <summary>Beklenmedik başlayanların ortalama takım tecrübesi (gözlenen takım-maç sayısı).</summary>
        public double ReplacementExperience { get; init; }
        /// <summary>Beklenmedik başlayanların ortalama başlangıç payı.</summary>
        public double ReplacementStartShare { get; init; }
        /// <summary>Beklenmedik başlayanların ortalama dakika payı; veri yoksa null.</summary>
        public double? ReplacementMinuteShare { get; init; }
        /// <summary>Eksik beklenen oyuncu profili − yerine başlayan profili (başlangıç payı ölçeğinde).</summary>
        public double ReplacementDeltaStartShare { get; init; }
        /// <summary>Aynı fark, dakika payı ölçeğinde; iki tarafta da dakika verisi yoksa null.</summary>
        public double? ReplacementDeltaMinuteShare { get; init; }

        public int BenchDepth { get; init; }
        /// <summary>Diziliş değişti mi? Diziliş verisi güvenilir DEĞİLSE null.</summary>
        public bool? FormationChanged { get; init; }
        /// <summary>Kullanım geçmişi hiç olmayan başlangıç oyuncusu sayısı.</summary>
        public int UnknownPlayerCount { get; init; }
        /// <summary>StartersOnly | WithBench | WithMinutes.</summary>
        public string LineupDataQuality { get; init; } = "StartersOnly";
        /// <summary>Beklenen 11 kurulabildi mi? Kurulamadıysa hiçbir özellik kullanılamaz.</summary>
        public bool ExpectedAvailable { get; init; }
        public int ObservedTeamMatches { get; init; }

        public static readonly LineupSideFeatures Unavailable = new() { ExpectedAvailable = false };
    }

    /// <summary>Maçın iki tarafının özellikleri.</summary>
    public sealed record LineupMatchFeatures(
        int MatchId, DateTime KickoffUtc, int LeagueId,
        LineupSideFeatures Home, LineupSideFeatures Away)
    {
        /// <summary>İki taraf da beklenen kadroya sahipse özellik kullanılabilir.</summary>
        public bool Usable => Home.ExpectedAvailable && Away.ExpectedAvailable;
    }

    /// <summary>
    /// ÖZELLİK ÇIKARICI — saf. Açıklanan kadro + beklenen kadro → özellikler.
    /// Beklenen kadro yoksa <see cref="LineupSideFeatures.Unavailable"/> döner (etki uygulanmaz).
    /// </summary>
    public static class LineupFeatureExtractor
    {
        /// <summary>"Düzenli oyuncu" eşiği — dönen oyuncu sayımında kullanılır.</summary>
        public const double RegularStartShare = 0.6;

        public static LineupSideFeatures Extract(
            IReadOnlyList<ExpectedLineupBuilder.LineupSlot> announced,
            ExpectedLineup? expected,
            IReadOnlyCollection<string>? previousMatchStarters,
            string? announcedFormation,
            string? previousFormation,
            string dataQuality)
        {
            if (expected == null) return LineupSideFeatures.Unavailable;

            var starters = announced.Where(s => s.Starter && !string.IsNullOrEmpty(s.PlayerKey)).ToList();
            var bench = announced.Count(s => !s.Starter && !string.IsNullOrEmpty(s.PlayerKey));
            if (starters.Count == 0) return LineupSideFeatures.Unavailable;

            var announcedKeys = starters.Select(s => s.PlayerKey).ToHashSet(StringComparer.Ordinal);
            var expectedKeys = expected.ExpectedStarters.ToHashSet(StringComparer.Ordinal);

            var intersection = announcedKeys.Intersect(expectedKeys, StringComparer.Ordinal).Count();
            var union = announcedKeys.Union(expectedKeys, StringComparer.Ordinal).Count();
            var missing = expectedKeys.Except(announcedKeys, StringComparer.Ordinal).ToList();
            var added = announcedKeys.Except(expectedKeys, StringComparer.Ordinal).ToList();

            // Mevki grubu değişimleri: mevki BİLİNEN oyuncular üzerinden. Bilinmeyen mevki sayılmaz.
            int Unit(string group) =>
                missing.Count(k => Group(expected.UsageOf(k).Position) == group) +
                added.Count(k => Group(PositionOf(starters, k) ?? expected.UsageOf(k).Position) == group);

            var expectedKeeper = expected.ExpectedStarters
                .FirstOrDefault(k => Group(expected.UsageOf(k).Position) == "G");
            var keeperChanged = expectedKeeper != null && !announcedKeys.Contains(expectedKeeper);

            var startShares = starters.Select(s => expected.UsageOf(s.PlayerKey).StartShare).ToList();
            var minuteShares = starters.Select(s => expected.UsageOf(s.PlayerKey).MinuteShare)
                .Where(m => m.HasValue).Select(m => m!.Value).ToList();
            var weighted = starters.Select(s => expected.UsageOf(s.PlayerKey).WeightedStartShare).ToList();

            var addedUsage = added.Select(k => expected.UsageOf(k)).ToList();
            var missingUsage = missing.Select(k => expected.UsageOf(k)).ToList();

            var addedMinutes = addedUsage.Where(u => u.MinuteShare.HasValue).Select(u => u.MinuteShare!.Value).ToList();
            var missingMinutes = missingUsage.Where(u => u.MinuteShare.HasValue).Select(u => u.MinuteShare!.Value).ToList();

            var returning = previousMatchStarters == null
                ? 0
                : announcedKeys.Count(k => !previousMatchStarters.Contains(k, StringComparer.Ordinal)
                                        && expected.UsageOf(k).StartShare >= RegularStartShare);

            // Diziliş yalnız İKİ tarafta da gerçekten yayımlanmışsa karşılaştırılır.
            bool? formationChanged = string.IsNullOrWhiteSpace(announcedFormation) || string.IsNullOrWhiteSpace(previousFormation)
                ? null
                : !string.Equals(announcedFormation.Trim(), previousFormation.Trim(), StringComparison.Ordinal);

            return new LineupSideFeatures
            {
                ExpectedAvailable = true,
                ObservedTeamMatches = expected.ObservedTeamMatches,
                StartingXiContinuity = intersection / 11.0,
                StarterJaccard = union == 0 ? 0 : intersection / (double)union,
                MissingExpectedStarters = missing.Count,
                AddedUnexpectedStarters = added.Count,
                GoalkeeperChanged = keeperChanged,
                DefensiveUnitChanges = Unit("D"),
                MidfieldUnitChanges = Unit("M"),
                AttackingUnitChanges = Unit("F"),
                ReturningRegulars = returning,
                MeanStarterStartShare = startShares.Count == 0 ? 0 : startShares.Average(),
                MeanStarterMinuteShare = minuteShares.Count == 0 ? null : minuteShares.Average(),
                WeightedStarterShare = weighted.Count == 0 ? 0 : weighted.Average(),
                ReplacementExperience = addedUsage.Count == 0 ? 0 : addedUsage.Average(u => u.Observations),
                ReplacementStartShare = addedUsage.Count == 0 ? 0 : addedUsage.Average(u => u.StartShare),
                ReplacementMinuteShare = addedMinutes.Count == 0 ? null : addedMinutes.Average(),
                // Eksik beklenen oyuncu ile yerine başlayanın profil farkı. Taraflardan biri boşsa 0:
                // "eksik yok" ile "yedek bilinmiyor" arasında fark UYDURULMAZ.
                ReplacementDeltaStartShare = missingUsage.Count == 0 || addedUsage.Count == 0
                    ? 0
                    : missingUsage.Average(u => u.StartShare) - addedUsage.Average(u => u.StartShare),
                ReplacementDeltaMinuteShare = missingMinutes.Count == 0 || addedMinutes.Count == 0
                    ? null
                    : missingMinutes.Average() - addedMinutes.Average(),
                BenchDepth = bench,
                FormationChanged = formationChanged,
                UnknownPlayerCount = starters.Count(s => !expected.UsageOf(s.PlayerKey).Known),
                LineupDataQuality = dataQuality
            };
        }

        private static string? PositionOf(IReadOnlyList<ExpectedLineupBuilder.LineupSlot> slots, string key)
            => slots.FirstOrDefault(s => string.Equals(s.PlayerKey, key, StringComparison.Ordinal))?.Position;

        /// <summary>G / D / M / F; tanınmayan ya da eksik mevki null (gruba SAYILMAZ).</summary>
        public static string? Group(string? position) => LineupPositions.Normalize(position);
    }
}
