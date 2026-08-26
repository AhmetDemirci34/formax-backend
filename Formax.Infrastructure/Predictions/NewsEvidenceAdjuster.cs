using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Formax.Application.Services.News.Intelligence;

namespace Formax.Infrastructure.Predictions
{
    /// <summary>
    /// NEWS_ADJUST_V1 — Shadow B'nin olasılık düzeltmesi.
    ///
    /// TAMAMEN DETERMİNİSTİK BACKEND MATEMATİĞİ. Buraya hiçbir LLM girmez ve giremez: bu sınıfın
    /// tek girdisi, kural-tabanlı Evidence katmanının çoktan ürettiği alanlardır (EventType,
    /// RelatedTeam, Timing, SourceQuality, Confidence, SourceCount). "Home %7 düşsün" kararını
    /// veren bir model yoktur; aşağıdaki formül vardır ve her satır kendi girdileriyle saklanır.
    ///
    /// FORMÜL
    ///   1. Her kanıt bir tarafa (HOME/AWAY) ve bir ağırlığa çevrilir:
    ///        w = typeWeight × qualityFactor × confidenceFactor × sourceFactor
    ///   2. Taraf başına ağırlıklar toplanır ve tavanlanır (tek haber seli sonucu ele geçiremez).
    ///   3. delta = awayImpact − homeImpact          (pozitif = deplasman daha çok zarar görmüş)
    ///   4. tilt  = clamp(K × delta, ±MaxTilt)
    ///   5. Log-odds kaydırması, ardından normalizasyon:
    ///        pH' = pH·e^(+tilt/2) ,  pA' = pA·e^(−tilt/2) ,  pD' = pD
    ///
    /// Beraberlik doğrudan itilmez; iki taraf arasındaki fark açıldıkça normalizasyonla
    /// kendiliğinden küçülür, kapandıkça büyür. Sıfır kanıt → tilt = 0 → olasılıklar Shadow A
    /// ile BİREBİR aynı kalır (deneyin "adjustment yapılmadı" kolu).
    /// </summary>
    public sealed class NewsAdjustOptions
    {
        /// <summary>Kural sürümü — PredictionId'ye girer; değişirse yeni tahmin üretilir.</summary>
        public const string Version = "NEWS_ADJUST_V1";

        /// <summary>Kanıt ağırlığını tilt'e çeviren katsayı.</summary>
        public double K { get; set; } = 0.35;

        /// <summary>Log-odds kaydırmasının mutlak tavanı. 0.40 ≈ en fazla ~%10 puanlık kayma.</summary>
        public double MaxTilt { get; set; } = 0.40;

        /// <summary>Tek taraf için birikebilecek en yüksek kanıt ağırlığı.</summary>
        public double MaxImpactPerSide { get; set; } = 1.0;

        /// <summary>Bu kaynak kalitesinin altındaki kanıt düzeltmeye GİREMEZ.</summary>
        public int MinSourceQuality { get; set; } = 85;

        /// <summary>Bu güvenin altındaki kanıt düzeltmeye GİREMEZ.</summary>
        public int MinConfidence { get; set; } = 60;

        /// <summary>Olasılık tabanı — düzeltme hiçbir sonucu sıfıra itemez.</summary>
        public double ProbabilityFloor { get; set; } = 0.001;
    }

    public sealed class AdjustmentInput
    {
        public required int EvidenceId { get; init; }
        public required string ContentHash { get; init; }
        public required string EventType { get; init; }
        public required string RelatedTeam { get; init; }
        public required string Timing { get; init; }
        public required string Source { get; init; }
        public required int SourceQuality { get; init; }
        public required int Confidence { get; init; }
        public required int SourceCount { get; init; }
        public required DateTime PublishedUtc { get; init; }
    }

    public sealed class UsedEvidence
    {
        public required AdjustmentInput Evidence { get; init; }
        public required string Side { get; init; }
        public required double Weight { get; init; }
    }

    public sealed class AdjustmentResult
    {
        public double HomeProbability { get; set; }
        public double DrawProbability { get; set; }
        public double AwayProbability { get; set; }
        public double HomeImpact { get; set; }
        public double AwayImpact { get; set; }
        public double AppliedTilt { get; set; }
        public bool AdjustmentApplied { get; set; }
        public string Reason { get; set; } = string.Empty;
        public List<UsedEvidence> Used { get; } = new();
        public DateTime? LatestEvidenceUtc { get; set; }
    }

    public static class NewsEvidenceAdjuster
    {
        /// <summary>
        /// KADRO ETKİLEYEN kanıt türleri ve ağırlıkları. Başka hiçbir tür düzeltmeye giremez:
        /// transfer söylentisi, teknik direktör açıklaması, maç önizlemesi, hava durumu vb.
        /// bir oyuncunun o maçta oynayıp oynamayacağını KANITLAMAZ.
        /// </summary>
        private static readonly Dictionary<string, double> TypeWeight = new(StringComparer.Ordinal)
        {
            [MatchIntelligenceService.EventTypes.Injury] = 1.00,
            [MatchIntelligenceService.EventTypes.Suspension] = 1.00,
            [MatchIntelligenceService.EventTypes.Squad] = 0.60,
            [MatchIntelligenceService.EventTypes.Lineup] = 0.45
        };

        public static bool IsSquadAffecting(string eventType) => TypeWeight.ContainsKey(eventType);

        /// <summary>
        /// Shadow A'nın olasılıklarını, verilen maç öncesi kanıtla düzeltir.
        /// Kanıt yoksa veya hiçbiri kapılardan geçmezse SONUÇ Shadow A ile birebir aynıdır.
        /// </summary>
        public static AdjustmentResult Apply(
            double baseHome, double baseDraw, double baseAway,
            string homeTeamName, string awayTeamName,
            DateTime kickoffUtc,
            IEnumerable<AdjustmentInput> evidence,
            NewsAdjustOptions opt)
        {
            var result = new AdjustmentResult
            {
                HomeProbability = baseHome,
                DrawProbability = baseDraw,
                AwayProbability = baseAway
            };

            double homeImpact = 0, awayImpact = 0;

            foreach (var e in evidence.OrderBy(x => x.PublishedUtc).ThenBy(x => x.EvidenceId))
            {
                // KAPI 1 — maç başlamadan önce yayımlanmış olmalı. Maç başladıktan sonra gelen
                // haber bu tahmine ASLA giremez; deneyin tamamı bu şarta dayanır.
                if (e.PublishedUtc >= kickoffUtc) continue;

                // KAPI 2 — zaman konumu: yalnız maç öncesi / maç günü. Eski ve maç sonrası düşer.
                if (e.Timing != MatchIntelligenceService.Timing.PreMatch &&
                    e.Timing != MatchIntelligenceService.Timing.MatchDay) continue;

                // KAPI 3 — yalnız kadro etkileyen tür.
                if (!TypeWeight.TryGetValue(e.EventType, out var typeWeight)) continue;

                // KAPI 4 — kaynak kalitesi ve güven.
                if (e.SourceQuality < opt.MinSourceQuality) continue;
                if (e.Confidence < opt.MinConfidence) continue;

                // KAPI 5 — hangi takım? Çözülemiyorsa kanıt TARAFSIZDIR ve kullanılmaz.
                var side = ResolveSide(e.RelatedTeam, homeTeamName, awayTeamName);
                if (side is null) continue;

                var qualityFactor = Math.Clamp(e.SourceQuality / 100.0, 0.0, 1.0);
                var confidenceFactor = Math.Clamp(e.Confidence / 100.0, 0.0, 1.0);
                // Çok kaynaklı haber daha ağır basar ama doğrusal değil: 1 kaynak 1.0, 3 kaynak ~1.2.
                var sourceFactor = 1.0 + Math.Min(0.5, Math.Log(Math.Max(1, e.SourceCount)) / 5.0);

                var w = typeWeight * qualityFactor * confidenceFactor * sourceFactor;

                if (side == "HOME") homeImpact += w; else awayImpact += w;
                result.Used.Add(new UsedEvidence { Evidence = e, Side = side, Weight = Round(w) });

                if (result.LatestEvidenceUtc is null || e.PublishedUtc > result.LatestEvidenceUtc)
                    result.LatestEvidenceUtc = e.PublishedUtc;
            }

            homeImpact = Math.Min(homeImpact, opt.MaxImpactPerSide);
            awayImpact = Math.Min(awayImpact, opt.MaxImpactPerSide);

            result.HomeImpact = Round(homeImpact);
            result.AwayImpact = Round(awayImpact);

            var delta = awayImpact - homeImpact;
            var tilt = Math.Clamp(opt.K * delta, -opt.MaxTilt, opt.MaxTilt);
            result.AppliedTilt = Round(tilt);

            if (result.AppliedTilt == 0.0)
            {
                result.AdjustmentApplied = false;
                result.Reason = result.Used.Count == 0
                    ? "Maç öncesi kadro kanıtı yok — Shadow A ile birebir aynı."
                    : "Kanıt iki tarafta da dengelendi — kayma yok.";
                return result;
            }

            var half = result.AppliedTilt / 2.0;
            var pH = baseHome * Math.Exp(+half);
            var pA = baseAway * Math.Exp(-half);
            var pD = baseDraw;

            var sum = pH + pD + pA;
            pH /= sum; pD /= sum; pA /= sum;

            // Taban: düzeltme hiçbir sonucu yok edemez.
            pH = Math.Max(pH, opt.ProbabilityFloor);
            pD = Math.Max(pD, opt.ProbabilityFloor);
            pA = Math.Max(pA, opt.ProbabilityFloor);
            sum = pH + pD + pA;

            result.HomeProbability = pH / sum;
            result.DrawProbability = pD / sum;
            result.AwayProbability = pA / sum;
            result.AdjustmentApplied = true;
            result.Reason = BuildReason(result, homeTeamName, awayTeamName);

            return result;
        }

        /// <summary>
        /// Kanıtın hangi tarafa ait olduğu. Kural-tabanlı ad eşleştirmesi — Evidence katmanının
        /// çözdüğü <c>RelatedTeam</c> ile maçın takım adları karşılaştırılır. Belirsizse null:
        /// tahmin edilmez, kanıt kullanılmaz.
        /// </summary>
        private static string? ResolveSide(string relatedTeam, string homeTeamName, string awayTeamName)
        {
            if (string.IsNullOrWhiteSpace(relatedTeam)) return null;

            var r = Fold(relatedTeam);
            var h = Fold(homeTeamName);
            var a = Fold(awayTeamName);
            if (r.Length == 0) return null;

            var matchesHome = h.Length > 0 && (r == h || r.Contains(h, StringComparison.Ordinal) || h.Contains(r, StringComparison.Ordinal));
            var matchesAway = a.Length > 0 && (r == a || r.Contains(a, StringComparison.Ordinal) || a.Contains(r, StringComparison.Ordinal));

            // İkisine birden benziyorsa (ör. ortak kelime) taraf belirsizdir — kullanılmaz.
            if (matchesHome == matchesAway) return null;
            return matchesHome ? "HOME" : "AWAY";
        }

        /// <summary>
        /// Takım adı karşılaştırması için sadeleştirme: Türkçe diyakritikler açık bir haritayla
        /// çevrilir ve yalnız harf/rakam bırakılır.
        ///
        /// NEDEN ELLE HARİTA: Türkçe'de <c>"İ".ToLowerInvariant()</c> "i̇" (i + birleşen nokta)
        /// üretir; bu, "İstanbul" ile "istanbul"u eşitsiz yapar ve daha önce market
        /// sınıflandırmasını sessizce kırmıştı. Kültüre bağlı büyük/küçük harf dönüşümü bu
        /// yolda kullanılmaz.
        /// </summary>
        private static string Fold(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            var sb = new StringBuilder(s.Length);
            foreach (var ch in s)
            {
                var c = ch switch
                {
                    'İ' or 'I' or 'ı' or 'i' => 'i',
                    'Ş' or 'ş' => 's',
                    'Ğ' or 'ğ' => 'g',
                    'Ü' or 'ü' => 'u',
                    'Ö' or 'ö' => 'o',
                    'Ç' or 'ç' => 'c',
                    'Â' or 'â' => 'a',
                    'Î' or 'î' => 'i',
                    'Û' or 'û' => 'u',
                    _ => char.ToLowerInvariant(ch)
                };
                if (char.IsLetterOrDigit(c)) sb.Append(c);
            }
            return sb.ToString();
        }

        private static string BuildReason(AdjustmentResult r, string homeTeamName, string awayTeamName)
        {
            var sb = new StringBuilder();
            var byType = r.Used
                .GroupBy(u => (u.Side, u.Evidence.EventType))
                .OrderByDescending(g => g.Sum(x => x.Weight));

            foreach (var g in byType)
            {
                if (sb.Length > 0) sb.Append("; ");
                var team = g.Key.Side == "HOME" ? homeTeamName : awayTeamName;
                sb.Append(CultureInfo.InvariantCulture,
                    $"{team}: {g.Key.EventType} x{g.Count()} (ağırlık {g.Sum(x => x.Weight):F3})");
            }

            sb.Append(CultureInfo.InvariantCulture,
                $" | homeImpact={r.HomeImpact:F3} awayImpact={r.AwayImpact:F3} tilt={r.AppliedTilt:F4}");
            return sb.ToString();
        }

        /// <summary>
        /// Determinizm şartı: kayan nokta gürültüsü PredictionId'yi oynatmamalı. Tüm ara
        /// değerler aynı ondalıkta yuvarlanır, id de bu yuvarlanmış değerden üretilir.
        /// </summary>
        private static double Round(double v) => Math.Round(v, 6, MidpointRounding.ToEven);
    }
}
