using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Application.Services.Outcomes;

namespace Formax.Application.Services.Lineups
{
    /// <summary>
    /// SNAPSHOT'IN KADRO BLOĞU — kadro öncesi ve sonrası olasılıklar yan yana saklanır.
    ///
    /// <see cref="Applied"/> false iken <c>LineupAdjusted*</c> değerleri <c>Base*</c> ile BİREBİR aynıdır:
    /// kadro yoksa ya da katman üretimde değilse temel model yüzdeleri değişmez. Üç olasılığın toplamı
    /// her durumda tam 1'dir.
    /// </summary>
    public sealed class OutcomeLineupDto
    {
        public string Version { get; set; } = LineupImpactVersion.Current;
        public string PolicyVersion { get; set; } = LineupImpactPolicy.Version;

        /// <summary>Kadro deltası YAYIMLANAN olasılığa uygulandı mı?</summary>
        public bool Applied { get; set; }

        /// <summary>Missing | Partial | Verified.</summary>
        public string LineupSourceStatus { get; set; } = LineupSourceStatuses.Missing;

        /// <summary>None | Insufficient | Low | Medium | High.</summary>
        public string LineupConfidence { get; set; } = LineupConfidenceLevels.None;

        public double BaseHomeProbability { get; set; }
        public double BaseDrawProbability { get; set; }
        public double BaseAwayProbability { get; set; }

        public double LineupAdjustedHomeProbability { get; set; }
        public double LineupAdjustedDrawProbability { get; set; }
        public double LineupAdjustedAwayProbability { get; set; }

        /// <summary>λ_ev üzerindeki log delta (uygulanmadıysa hesaplanan değer yine taşınır — denetim için).</summary>
        public double HomeLineupDelta { get; set; }
        public double AwayLineupDelta { get; set; }

        public int ResolvedStarters { get; set; }
        public int TotalStarters { get; set; }

        /// <summary>Muhtemel yedek havuzunun ortalama kalitesi (hücum/savunma log ölçeği).</summary>
        public double ReplacementQualityAttack { get; set; }
        public double ReplacementQualityDefence { get; set; }

        /// <summary>Etkisi ölçülebilen başlangıç oyuncuları (isim + net etki).</summary>
        public List<OutcomeLineupPlayerDto> ImpactfulStarters { get; set; } = new();
        /// <summary>Ölçülebilir etkiye sahip olup bu maçta ilk 11'de olmayanlar.</summary>
        public List<OutcomeLineupPlayerDto> ImpactfulAbsences { get; set; } = new();

        public List<string> AdjustmentReasonCodes { get; set; } = new();

        /// <summary>Üç olasılığın toplamı 1 mi (yayın öncesi değişmez kontrol)?</summary>
        public bool SumsToOne(double tolerance = 1e-9)
            => Math.Abs(BaseHomeProbability + BaseDrawProbability + BaseAwayProbability - 1) <= tolerance
            && Math.Abs(LineupAdjustedHomeProbability + LineupAdjustedDrawProbability + LineupAdjustedAwayProbability - 1) <= tolerance;

        /// <summary>
        /// Kadro bloğunu kurar. <paramref name="adjusted"/> null ise (katman kapalı ya da delta yok)
        /// düzeltilmiş olasılıklar tabanla aynı yazılır — sahte etki üretilmez.
        /// </summary>
        public static OutcomeLineupDto Build(
            LineupAdjustment adjustment,
            ScoreDistribution baseDistribution,
            ScoreDistribution? adjustedDistribution,
            bool appliedToPublished)
        {
            var (bh, bd, ba) = Normalize(baseDistribution);
            var (ah, ad, aa) = adjustedDistribution == null ? (bh, bd, ba) : Normalize(adjustedDistribution);
            var reasons = adjustment.AdjustmentReasonCodes.ToList();
            if (adjustment.Applied && !appliedToPublished && !reasons.Contains(LineupReasonCodes.ShadowNotProduction))
                reasons.Add(LineupReasonCodes.ShadowNotProduction);

            return new OutcomeLineupDto
            {
                Applied = appliedToPublished && adjustment.Applied,
                LineupSourceStatus = adjustment.LineupSourceStatus,
                LineupConfidence = adjustment.LineupConfidence,
                BaseHomeProbability = bh,
                BaseDrawProbability = bd,
                BaseAwayProbability = ba,
                LineupAdjustedHomeProbability = appliedToPublished ? ah : bh,
                LineupAdjustedDrawProbability = appliedToPublished ? ad : bd,
                LineupAdjustedAwayProbability = appliedToPublished ? aa : ba,
                HomeLineupDelta = adjustment.HomeLineupDelta,
                AwayLineupDelta = adjustment.AwayLineupDelta,
                ResolvedStarters = adjustment.ResolvedStarters,
                TotalStarters = adjustment.TotalStarters,
                ReplacementQualityAttack = adjustment.ReplacementQualityAttack,
                ReplacementQualityDefence = adjustment.ReplacementQualityDefence,
                ImpactfulStarters = adjustment.ImpactfulStarters.Select(OutcomeLineupPlayerDto.From).ToList(),
                ImpactfulAbsences = adjustment.ImpactfulAbsences.Select(OutcomeLineupPlayerDto.From).ToList(),
                AdjustmentReasonCodes = reasons
            };
        }

        /// <summary>Üç sonucun tam 1'e toplanması yapısal olarak garanti edilir (yuvarlama artığı ev sahibine yazılır).</summary>
        private static (double Home, double Draw, double Away) Normalize(ScoreDistribution d)
        {
            var h = d.HomeWin; var dr = d.Draw; var a = d.AwayWin;
            var sum = h + dr + a;
            if (sum <= 0) return (1.0 / 3, 1.0 / 3, 1.0 / 3);
            h /= sum; dr /= sum; a /= sum;
            h = Math.Round(h, 9); dr = Math.Round(dr, 9); a = Math.Round(a, 9);
            h = 1 - dr - a;
            return (h, dr, a);
        }
    }

    /// <summary>Kadro bloğundaki tek oyuncu satırı (denetim ve açıklanabilirlik).</summary>
    public sealed class OutcomeLineupPlayerDto
    {
        public string Name { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        /// <summary>Etkinin öğrenildiği başlangıç sayısı — küçük örneklem burada görülür.</summary>
        public int Matches { get; set; }
        public double NetAttack { get; set; }
        public double NetDefence { get; set; }

        public static OutcomeLineupPlayerDto From(LineupPlayerContribution c) => new()
        {
            Name = c.DisplayName,
            Position = c.Position,
            Matches = c.Matches,
            NetAttack = c.NetAttack,
            NetDefence = c.NetDefence
        };
    }
}
