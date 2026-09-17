using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Formax.Domain.Constants;

namespace Formax.Application.Services.Outcomes
{
    /// <summary>Olasılık aileleri.</summary>
    public static class OutcomeFamilies
    {
        public const string Result = "MatchResult";
        public const string Goals = "TotalGoals";
        public const string Btts = "BothTeamsScore";
        public const string Other = "Other";

        public static string Title(string family) => family switch
        {
            Result => "Maç Sonucu",
            Goals => "Gol Beklentisi",
            Btts => "İki Takımın Gol Durumu",
            _ => "Diğer"
        };

        /// <summary>Ana kartlara giremeyen (çifte şans gibi bileşik) marketler.</summary>
        public static bool IsCompound(string marketKey)
            => marketKey is OddsMarketKeys.DoubleChance1X or OddsMarketKeys.DoubleChanceX2 or OddsMarketKeys.DoubleChance12;
    }

    public sealed class OutcomeCandidateDto
    {
        public string Family { get; set; } = string.Empty;
        public string FamilyTitle { get; set; } = string.Empty;
        public string Market { get; set; } = string.Empty;
        /// <summary>Seçim/settlement anahtarı; seçilemeyen market için null.</summary>
        public string? MarketKey { get; set; }
        /// <summary>Kullanıcıya gösterilen yüzde (kalibre, ailede yuvarlama tutarlı).</summary>
        public int Probability { get; set; }
        public double RawProbability { get; set; }
        public double CalibratedProbability { get; set; }
        public double BaselineProbability { get; set; }
        public double InformationLift { get; set; }
        public double EvidenceCoverage { get; set; }
        public string SampleQuality { get; set; } = string.Empty;
        public double Uncertainty { get; set; }
        public double SelectionScore { get; set; }
        public List<string> ReasonCodes { get; set; } = new();
        public string? Reason { get; set; }
        public string? Limitation { get; set; }
        /// <summary>Adayın ait olduğu snapshot ve model sürümü (seçim skoru denetimi için aday düzeyinde taşınır).</summary>
        public string? SnapshotId { get; set; }
        public string ModelVersion { get; set; } = OutcomeModelVersion.Current;
        public string SelectionVersion { get; set; } = OutcomeSnapshotBuilder.SelectionVersion;
    }

    public sealed class OutcomeFamilyDto
    {
        public string Family { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public List<OutcomeCandidateDto> Items { get; set; } = new();
    }

    public sealed class OutcomeScoreDto
    {
        public int Home { get; set; }
        public int Away { get; set; }
        public int Probability { get; set; }
    }

    public sealed class OutcomeChecksDto
    {
        public double ResultSum { get; set; }
        public double BttsSum { get; set; }
        public double Over15Sum { get; set; }
        public double Over25Sum { get; set; }
        public double Over35Sum { get; set; }
        public double DoubleChance1XError { get; set; }
        public double DoubleChanceX2Error { get; set; }
        public double DoubleChance12Error { get; set; }
        public bool Consistent { get; set; }
    }

    /// <summary>Snapshot yükü — Keşfet ve Maç Detayı bu nesnenin AYNISINI okur.</summary>
    public sealed class OutcomeSnapshotDto
    {
        public string? SnapshotId { get; set; }
        public int MatchId { get; set; }
        public string ModelVersion { get; set; } = OutcomeModelVersion.Current;
        public string? CalibrationRunId { get; set; }
        public DateTime? ComputedAtUtc { get; set; }
        public DateTime? InputsCutoffUtc { get; set; }
        /// <summary>Available | InsufficientData | Pending.</summary>
        public string Status { get; set; } = "Pending";
        public double? ExpectedHomeGoals { get; set; }
        public double? ExpectedAwayGoals { get; set; }
        public double EvidenceCoverage { get; set; }
        public string SampleQuality { get; set; } = string.Empty;
        public int HomeSampleSize { get; set; }
        public int AwaySampleSize { get; set; }
        public string? Limitation { get; set; }
        public List<OutcomeCandidateDto> MainCards { get; set; } = new();
        public List<OutcomeFamilyDto> Families { get; set; } = new();
        public List<OutcomeScoreDto> TopScores { get; set; } = new();
        public List<string> ReasonCodes { get; set; } = new();
        public OutcomeChecksDto? Checks { get; set; }
        public string? Notice { get; set; }

        /// <summary>Enabled | Limited | Disabled. Yüzdeler kullanıcıya YALNIZ Enabled'da gider.</summary>
        public string PredictionEligibility { get; set; } = PredictionEligibilities.Disabled;
        public List<string> EligibilityReasons { get; set; } = new();
        public string? TriggerType { get; set; }
        public string? PreviousSnapshotId { get; set; }
        /// <summary>Ligler arası maç bilgisi (ortak güç ölçeği) — teşhis için.</summary>
        public OutcomeStrengthDto? Strength { get; set; }
    }

    public sealed class OutcomeStrengthDto
    {
        public bool CrossLeague { get; set; }
        public int? HomeLeagueId { get; set; }
        public int? AwayLeagueId { get; set; }
        public double HomeLeagueStrength { get; set; }
        public double AwayLeagueStrength { get; set; }
        public int HomeLeagueLinks { get; set; }
        public int AwayLeagueLinks { get; set; }
        public double HomeClubRating { get; set; }
        public double AwayClubRating { get; set; }
        public double LambdaHome { get; set; }
        public double LambdaAway { get; set; }
        public double EloHomeExpectation { get; set; }
    }

    /// <summary>
    /// SNAPSHOT KURUCU — dağılımlardan bütün aileleri, tutarlılık kontrollerini, gerekçe kodlarını ve ana üç kartı üretir.
    ///
    /// ANA KART KURALI: üç kart üç FARKLI aileden (Maç Sonucu / Gol Beklentisi / İki Takımın Gol Durumu). Çifte şans (1X, X2, 12)
    /// bileşik olasılıktır — P(1X)=P(1)+P(X) — ve tek olaylardan yapısal olarak yüksek çıkar; ana kartlara ASLA giremez, yalnız
    /// "Tüm Olasılıklar" içinde gösterilir. Aile içinde seçim ham yüzdeye göre değil: kalibre olasılık + lig tabanına göre bilgi
    /// farkı (standartlaştırılmış) + veri kapsamıyla yapılır. Oran (bookmaker) hiçbir adımda kullanılmaz.
    /// </summary>
    public static class OutcomeSnapshotBuilder
    {
        /// <summary>Ana kart seçim kuralının sürümü — değişince snapshot'lar yeniden üretilir.</summary>
        public const string SelectionVersion = "selection-2";

        public const string InsufficientNotice = "Bu maç için olası sonuç üretecek yeterli doğrulanmış veri bulunamadı.";
        /// <summary>Limited / Disabled maçlarda kullanıcıya gösterilen TEK metin (yüzde yok).</summary>
        public const string NotEligibleNotice = "Bu maç için güvenilir AI beklentisi oluşturacak yeterli doğrulanmış veri bulunmuyor.";

        /// <summary>Ev sahibi/deplasman/lig bilgisi olmadan kapanan (yayımlanmayan) kapı kodları — Disabled.</summary>
        public static readonly IReadOnlySet<string> HardGates = new HashSet<string>
        {
            "INSUFFICIENT_SAMPLE", "TEAM_LEAGUE_UNKNOWN", "CROSS_LEAGUE_UNLINKED", "LEAGUE_NOT_EVALUATED", "MATCH_NOT_SCHEDULED"
        };

        /// <summary>
        /// UYGUNLUK BİRLEŞTİRME — maç kapıları + lig sınavı. Sert kapı (veri/kimlik/bağlantı yok, maç ertelendi) → Disabled;
        /// çıktı kapısı (kanıtsız aşırı olasılık, bağımsız reyting çelişkisi) → en fazla Limited; aksi hâlde lig kararı.
        /// </summary>
        public static (string Eligibility, List<string> Reasons) Combine(string? leagueStatus, IEnumerable<string> leagueReasons, IEnumerable<string> matchGates, IEnumerable<string> outputGates)
        {
            var reasons = new List<string>();
            var gates = matchGates.ToList();
            reasons.AddRange(gates);
            if (leagueStatus == null) reasons.Add("LEAGUE_NOT_EVALUATED");
            if (reasons.Any(HardGates.Contains)) return (PredictionEligibilities.Disabled, reasons);
            var outs = outputGates.ToList();
            reasons.AddRange(outs);
            reasons.AddRange(leagueReasons.Select(r => "LEAGUE:" + r));
            if (leagueStatus == PredictionEligibilities.Disabled) return (PredictionEligibilities.Disabled, reasons);
            if (outs.Count > 0 || leagueStatus == PredictionEligibilities.Limited) return (PredictionEligibilities.Limited, reasons);
            return (PredictionEligibilities.Enabled, reasons);
        }

        /// <summary>
        /// KULLANICI GÖRÜNÜMÜ — Enabled değilse yüzde, aile, skor ve beklenen gol TAŞINMAZ (frontend gösteremez); yalnız durum, gerekçe
        /// kodları, SnapshotId/ModelVersion/hesaplama zamanı ve dürüst metin kalır. Keşfet ve Detay aynı temizlenmiş nesneyi okur.
        /// </summary>
        public static OutcomeSnapshotDto ForUser(OutcomeSnapshotDto s)
        {
            if (s.Status == "Pending" || s.PredictionEligibility == PredictionEligibilities.Enabled) return s;
            return new OutcomeSnapshotDto
            {
                SnapshotId = s.SnapshotId, MatchId = s.MatchId, ModelVersion = s.ModelVersion, CalibrationRunId = s.CalibrationRunId,
                ComputedAtUtc = s.ComputedAtUtc, InputsCutoffUtc = s.InputsCutoffUtc,
                Status = "NotEligible",
                PredictionEligibility = s.PredictionEligibility,
                EligibilityReasons = s.EligibilityReasons,
                EvidenceCoverage = s.EvidenceCoverage, SampleQuality = s.SampleQuality,
                HomeSampleSize = s.HomeSampleSize, AwaySampleSize = s.AwaySampleSize,
                ReasonCodes = s.ReasonCodes, TriggerType = s.TriggerType, PreviousSnapshotId = s.PreviousSnapshotId,
                Notice = NotEligibleNotice
            };
        }

        private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

        public static OutcomeSnapshotDto Insufficient(int matchId, OutcomeExpectation e, string homeName, string awayName) => new()
        {
            MatchId = matchId,
            Status = "InsufficientData",
            EvidenceCoverage = Math.Round(e.Coverage, 3),
            SampleQuality = SampleQuality(e),
            HomeSampleSize = e.HomeSample,
            AwaySampleSize = e.AwaySample,
            Limitation = $"Doğrulanmış geçmiş maç sayısı yetersiz: {homeName} {e.HomeSample}, {awayName} {e.AwaySample}.",
            Notice = NotEligibleNotice,
            ReasonCodes = new List<string> { "INSUFFICIENT_SAMPLE" },
            PredictionEligibility = PredictionEligibilities.Disabled,
            EligibilityReasons = e.GateReasons.Count > 0 ? e.GateReasons.ToList() : new List<string> { "INSUFFICIENT_SAMPLE" }
        };

        public static string SampleQuality(OutcomeExpectation e)
            => e.Coverage >= 0.99 ? "Rich" : e.Coverage >= 0.6 ? "Developing" : "Limited";

        public static OutcomeSnapshotDto Build(int matchId, OutcomePrediction p, string homeName, string awayName)
        {
            var e = p.Expectation;
            var cal = p.Calibrated;
            var raw = p.Raw;
            var bas = p.Baseline;
            var quality = SampleQuality(e);
            var uncertainty = Math.Round(1 - e.Coverage, 3);
            string? limitation = e.Coverage < 0.6
                ? $"Sınırlı veri: {homeName} için {e.HomeSample}, {awayName} için {e.AwaySample} doğrulanmış maç; yüzdeler lig ortalamasına yaklaştırıldı."
                : null;

            OutcomeCandidateDto C(string family, string market, string? key, double rawP, double calP, double baseP)
            {
                var lift = calP - baseP;
                return new OutcomeCandidateDto
                {
                    Family = family, FamilyTitle = OutcomeFamilies.Title(family), Market = market, MarketKey = key,
                    RawProbability = Math.Round(rawP, 4), CalibratedProbability = Math.Round(calP, 4), BaselineProbability = Math.Round(baseP, 4),
                    InformationLift = Math.Round(lift, 4), EvidenceCoverage = Math.Round(e.Coverage, 3), SampleQuality = quality,
                    Uncertainty = uncertainty, Limitation = limitation
                };
            }

            var result = new List<OutcomeCandidateDto>
            {
                C(OutcomeFamilies.Result, "Ev Sahibi Kazanır", OddsMarketKeys.Ms1, raw.HomeWin, cal.HomeWin, bas.HomeWin),
                C(OutcomeFamilies.Result, "Beraberlik", OddsMarketKeys.MsX, raw.Draw, cal.Draw, bas.Draw),
                C(OutcomeFamilies.Result, "Deplasman Kazanır", OddsMarketKeys.Ms2, raw.AwayWin, cal.AwayWin, bas.AwayWin)
            };
            var goals = new List<OutcomeCandidateDto>();
            foreach (var (line, over, under) in new[] { (1.5, OutcomeMarketKeys.Over15, OutcomeMarketKeys.Under15), (2.5, OddsMarketKeys.Over25, OddsMarketKeys.Under25), (3.5, OutcomeMarketKeys.Over35, OutcomeMarketKeys.Under35) })
            {
                var l = line.ToString("0.0", CultureInfo.InvariantCulture);
                goals.Add(C(OutcomeFamilies.Goals, $"{l} Üst", over, raw.Over(line), cal.Over(line), bas.Over(line)));
                goals.Add(C(OutcomeFamilies.Goals, $"{l} Alt", under, raw.Under(line), cal.Under(line), bas.Under(line)));
            }
            var btts = new List<OutcomeCandidateDto>
            {
                C(OutcomeFamilies.Btts, "Karşılıklı Gol Var", OddsMarketKeys.BttsYes, raw.BttsYes, cal.BttsYes, bas.BttsYes),
                C(OutcomeFamilies.Btts, "Karşılıklı Gol Yok", OddsMarketKeys.BttsNo, raw.BttsNo, cal.BttsNo, bas.BttsNo)
            };
            var other = new List<OutcomeCandidateDto>
            {
                C(OutcomeFamilies.Other, "Çifte Şans (1X)", OddsMarketKeys.DoubleChance1X, raw.HomeWin + raw.Draw, cal.HomeWin + cal.Draw, bas.HomeWin + bas.Draw),
                C(OutcomeFamilies.Other, "Çifte Şans (X2)", OddsMarketKeys.DoubleChanceX2, raw.Draw + raw.AwayWin, cal.Draw + cal.AwayWin, bas.Draw + bas.AwayWin),
                C(OutcomeFamilies.Other, "Çifte Şans (1-2)", OddsMarketKeys.DoubleChance12, raw.HomeWin + raw.AwayWin, cal.HomeWin + cal.AwayWin, bas.HomeWin + bas.AwayWin),
                C(OutcomeFamilies.Other, $"{homeName} Gol Atar", null, raw.HomeScores, cal.HomeScores, bas.HomeScores),
                C(OutcomeFamilies.Other, $"{awayName} Gol Atar", null, raw.AwayScores, cal.AwayScores, bas.AwayScores),
                C(OutcomeFamilies.Other, $"{homeName} Gol Yemez", null, raw.HomeCleanSheet, cal.HomeCleanSheet, bas.HomeCleanSheet),
                C(OutcomeFamilies.Other, $"{awayName} Gol Yemez", null, raw.AwayCleanSheet, cal.AwayCleanSheet, bas.AwayCleanSheet),
                C(OutcomeFamilies.Other, "Toplam Gol 0-1", null, raw.TotalBetween(0, 1), cal.TotalBetween(0, 1), bas.TotalBetween(0, 1)),
                C(OutcomeFamilies.Other, "Toplam Gol 2-3", null, raw.TotalBetween(2, 3), cal.TotalBetween(2, 3), bas.TotalBetween(2, 3)),
                C(OutcomeFamilies.Other, "Toplam Gol 4+", null, raw.TotalBetween(4, 99), cal.TotalBetween(4, 99), bas.TotalBetween(4, 99))
            };

            // ── Gösterim yuvarlaması — aile içinde tutarlı (1X2 = 100; alt + üst = 100; KG var + yok = 100; çifte şans = bileşenler) ──
            var res = LargestRemainder(result.Select(r => r.CalibratedProbability).ToArray());
            for (var i = 0; i < 3; i++) result[i].Probability = res[i];
            for (var i = 0; i < goals.Count; i += 2)
            {
                goals[i].Probability = Pct(goals[i].CalibratedProbability);
                goals[i + 1].Probability = 100 - goals[i].Probability;
            }
            btts[0].Probability = Pct(btts[0].CalibratedProbability);
            btts[1].Probability = 100 - btts[0].Probability;
            other[0].Probability = res[0] + res[1];
            other[1].Probability = res[1] + res[2];
            other[2].Probability = res[0] + res[2];
            var band = LargestRemainder(new[] { other[7].CalibratedProbability, other[8].CalibratedProbability, other[9].CalibratedProbability });
            other[7].Probability = band[0]; other[8].Probability = band[1]; other[9].Probability = band[2];
            for (var i = 3; i <= 6; i++) other[i].Probability = Pct(other[i].CalibratedProbability);

            // ── Gerekçe kodları (gerçek model girdilerinden) ──
            var codes = ReasonCodes(e, cal);
            foreach (var c in result) { c.ReasonCodes = codes.Where(IsResultCode).ToList(); }
            foreach (var c in goals) { c.ReasonCodes = codes.Where(IsGoalCode).ToList(); }
            foreach (var c in btts) { c.ReasonCodes = codes.Where(IsBttsCode).ToList(); }

            // ── Ana kart seçimi ──
            foreach (var c in result.Concat(goals).Concat(btts)) c.SelectionScore = Math.Round(Score(c, e.Coverage), 4);
            var mainResult = result.OrderByDescending(c => c.CalibratedProbability).ThenByDescending(c => c.SelectionScore).First();
            var mainGoals = goals.Where(c => c.CalibratedProbability >= 0.5)
                .OrderByDescending(c => c.SelectionScore).ThenBy(c => c.Market.StartsWith("2.5") ? 0 : 1).First();
            var mainBtts = btts.OrderByDescending(c => c.CalibratedProbability).First();

            mainResult.Reason = ResultReason(mainResult, e, cal, homeName, awayName);
            mainGoals.Reason = GoalsReason(mainGoals, e, cal, homeName, awayName);
            mainBtts.Reason = BttsReason(mainBtts, cal, homeName, awayName);

            var checks = new OutcomeChecksDto
            {
                ResultSum = Math.Round(cal.HomeWin + cal.Draw + cal.AwayWin, 6),
                BttsSum = Math.Round(cal.BttsYes + cal.BttsNo, 6),
                Over15Sum = Math.Round(cal.Over(1.5) + cal.Under(1.5), 6),
                Over25Sum = Math.Round(cal.Over(2.5) + cal.Under(2.5), 6),
                Over35Sum = Math.Round(cal.Over(3.5) + cal.Under(3.5), 6),
                DoubleChance1XError = Math.Round(Math.Abs(other[0].CalibratedProbability - (result[0].CalibratedProbability + result[1].CalibratedProbability)), 6),
                DoubleChanceX2Error = Math.Round(Math.Abs(other[1].CalibratedProbability - (result[1].CalibratedProbability + result[2].CalibratedProbability)), 6),
                DoubleChance12Error = Math.Round(Math.Abs(other[2].CalibratedProbability - (result[0].CalibratedProbability + result[2].CalibratedProbability)), 6)
            };
            checks.Consistent = new[] { checks.ResultSum, checks.BttsSum, checks.Over15Sum, checks.Over25Sum, checks.Over35Sum }.All(s => Math.Abs(s - 1) < 0.002)
                                && checks.DoubleChance1XError < 0.001 && checks.DoubleChanceX2Error < 0.001 && checks.DoubleChance12Error < 0.001;

            return new OutcomeSnapshotDto
            {
                MatchId = matchId,
                Status = "Available",
                ExpectedHomeGoals = Math.Round(cal.ExpectedHome, 2),
                ExpectedAwayGoals = Math.Round(cal.ExpectedAway, 2),
                EvidenceCoverage = Math.Round(e.Coverage, 3),
                SampleQuality = quality,
                HomeSampleSize = e.HomeSample,
                AwaySampleSize = e.AwaySample,
                Limitation = limitation,
                MainCards = new List<OutcomeCandidateDto> { mainResult, mainGoals, mainBtts },
                Families = new List<OutcomeFamilyDto>
                {
                    new() { Family = OutcomeFamilies.Result, Title = OutcomeFamilies.Title(OutcomeFamilies.Result), Items = result },
                    new() { Family = OutcomeFamilies.Goals, Title = OutcomeFamilies.Title(OutcomeFamilies.Goals), Items = goals },
                    new() { Family = OutcomeFamilies.Btts, Title = OutcomeFamilies.Title(OutcomeFamilies.Btts), Items = btts },
                    new() { Family = OutcomeFamilies.Other, Title = OutcomeFamilies.Title(OutcomeFamilies.Other), Items = other }
                },
                TopScores = cal.TopScores(5).Select(s => new OutcomeScoreDto { Home = s.Home, Away = s.Away, Probability = Pct(s.P) }).ToList(),
                ReasonCodes = codes,
                Checks = checks
            };
        }

        /// <summary>
        /// SEÇİM SKORU — kalibre olasılık + lig tabanına göre standartlaştırılmış bilgi farkı, veri kapsamıyla ağırlıklı.
        /// Standartlaştırma (fark / √(p₀(1−p₀))) uç çizgilerin (1.5 Üst gibi tabanı zaten yüksek olaylar) yalnız yüksek
        /// yüzdeyle öne geçmesini engeller.
        /// </summary>
        public static double Score(OutcomeCandidateDto c, double coverage)
        {
            var b = Math.Clamp(c.BaselineProbability, 0.02, 0.98);
            var std = c.InformationLift / Math.Sqrt(b * (1 - b));
            // Ölçüm (15.09.2026, 1.718 test maçı): ham yüzde ağırlığı 0,35 iken gol kartının %98'i yüksek tabanlı "1.5 Üst"/"3.5 Alt"
            // çizgilerine düşüyordu (çifte şansın gol ailesindeki karşılığı). Ağırlık 0,10'a indirildi; bilgi farkı belirleyicidir.
            return 0.10 * (c.CalibratedProbability - 0.5) + std * (0.4 + 0.6 * coverage);
        }

        private static int Pct(double p) => (int)Math.Round(Math.Clamp(p, 0, 1) * 100, MidpointRounding.AwayFromZero);

        /// <summary>Toplamı 100 olan tamsayı yüzdeler (en büyük kalan yöntemi).</summary>
        public static int[] LargestRemainder(double[] probs)
        {
            var total = probs.Sum();
            if (total <= 0) return probs.Select(_ => 0).ToArray();
            var scaled = probs.Select(p => p / total * 100).ToArray();
            var floors = scaled.Select(s => (int)Math.Floor(s)).ToArray();
            var remaining = 100 - floors.Sum();
            foreach (var i in scaled.Select((s, i) => (r: s - Math.Floor(s), i)).OrderByDescending(x => x.r).ThenBy(x => x.i).Take(remaining).Select(x => x.i))
                floors[i]++;
            return floors;
        }

        private static bool IsResultCode(string c) => c.StartsWith("RESULT_") || c.StartsWith("SAMPLE_") || c == "HOME_ADVANTAGE_LEAGUE" || c == "STALE_RATING";
        private static bool IsGoalCode(string c) => c.StartsWith("GOALS_") || c.StartsWith("SAMPLE_") || c == "STALE_RATING";
        private static bool IsBttsCode(string c) => c.StartsWith("BTTS_") || c.StartsWith("SAMPLE_") || c == "STALE_RATING";

        public static List<string> ReasonCodes(OutcomeExpectation e, ScoreDistribution cal)
        {
            var codes = new List<string>();
            var diff = e.LambdaHome - e.LambdaAway;
            if (diff >= 0.6) codes.Add("RESULT_HOME_CLEAR_FAVOURITE");
            else if (diff >= 0.25) codes.Add("RESULT_HOME_STRONGER");
            else if (diff <= -0.6) codes.Add("RESULT_AWAY_CLEAR_FAVOURITE");
            else if (diff <= -0.25) codes.Add("RESULT_AWAY_STRONGER");
            else codes.Add("RESULT_BALANCED");
            if (e.LeagueHome - e.LeagueAway >= 0.2) codes.Add("HOME_ADVANTAGE_LEAGUE");

            var leagueTotal = e.LeagueHome + e.LeagueAway;
            var total = e.LambdaHome + e.LambdaAway;
            if (total <= leagueTotal * 0.85) codes.Add("GOALS_LOW_EXPECTATION");
            else if (total >= leagueTotal * 1.15) codes.Add("GOALS_HIGH_EXPECTATION");
            else codes.Add("GOALS_NEAR_LEAGUE_AVERAGE");

            if (cal.HomeScores >= 0.7 && cal.AwayScores >= 0.7) codes.Add("BTTS_BOTH_LIKELY_TO_SCORE");
            if (cal.AwayScores < 0.6) codes.Add("BTTS_AWAY_SCORING_DOUBT");
            if (cal.HomeScores < 0.6) codes.Add("BTTS_HOME_SCORING_DOUBT");
            if (!codes.Any(c => c.StartsWith("BTTS_"))) codes.Add("BTTS_BALANCED_SCORING");

            codes.Add(e.Coverage >= 0.99 ? "SAMPLE_RICH" : e.Coverage >= 0.6 ? "SAMPLE_DEVELOPING" : "SAMPLE_LIMITED");
            if (e.Coverage < 1 && Math.Min(e.HomeSample, e.AwaySample) >= 12) codes.Add("STALE_RATING");
            return codes;
        }

        private static string F(double v) => v.ToString("0.0", Tr);

        private static string ResultReason(OutcomeCandidateDto c, OutcomeExpectation e, ScoreDistribution cal, string home, string away)
        {
            var s = $"Model beklenen golü {home} {F(cal.ExpectedHome)}, {away} {F(cal.ExpectedAway)} olarak hesaplıyor";
            // Metin gerekçe KODUNDAN türetilir (kart ↔ kod çelişkisi olmasın): yakın beklentide "üstünlük" denmez.
            var codes = ReasonCodes(e, cal);
            s += c.MarketKey switch
            {
                OddsMarketKeys.Ms1 when codes.Any(x => x is "RESULT_HOME_STRONGER" or "RESULT_HOME_CLEAR_FAVOURITE")
                    => "; ev sahibinin reyting üstünlüğü bu sonuca en yüksek payı veriyor.",
                OddsMarketKeys.Ms2 when codes.Any(x => x is "RESULT_AWAY_STRONGER" or "RESULT_AWAY_CLEAR_FAVOURITE")
                    => "; deplasman takımının reyting üstünlüğü bu sonuca en yüksek payı veriyor.",
                OddsMarketKeys.Ms1 => "; iki takımın gol beklentisi birbirine yakın, ev sahibi sonucu küçük farkla öne çıkıyor.",
                OddsMarketKeys.Ms2 => "; iki takımın gol beklentisi birbirine yakın, deplasman sonucu küçük farkla öne çıkıyor.",
                _ => "; iki takımın gol beklentisi birbirine yakın, beraberlik payı yüksek."
            };
            if (e.CrossLeague)
                s += " Takımlar farklı liglerden geliyor; güçler ligler arası maçlardan öğrenilen ortak ölçekte karşılaştırıldı.";
            if (e.HomeRecentCount >= 5 && e.AwayRecentCount >= 5)
                s += $" Son {e.HomeRecentCount} maçta {home} maç başına {F(e.HomeRecentFor)} gol attı, {away} son {e.AwayRecentCount} maçta {F(e.AwayRecentFor)}.";
            return s;
        }

        private static string GoalsReason(OutcomeCandidateDto c, OutcomeExpectation e, ScoreDistribution cal, string home, string away)
        {
            var total = cal.ExpectedTotalGoals;
            var league = e.LeagueHome + e.LeagueAway;
            var s = $"Beklenen toplam gol {F(total)}; bu ligin ortalaması {F(league)}.";
            if (e.HomeRecentCount >= 5 && e.AwayRecentCount >= 5)
                s += $" {home} son {e.HomeRecentCount} maçında maç başına {F(e.HomeRecentFor + e.HomeRecentAgainst)}, {away} {F(e.AwayRecentFor + e.AwayRecentAgainst)} gol gördü.";
            return s;
        }

        private static string BttsReason(OutcomeCandidateDto c, ScoreDistribution cal, string home, string away)
            => $"Modele göre {home} için gol bulma olasılığı %{Pct(cal.HomeScores)}, {away} için %{Pct(cal.AwayScores)}.";
    }

    /// <summary>1.5 ve 3.5 çizgileri için seçim/settlement anahtarları (additive).</summary>
    public static class OutcomeMarketKeys
    {
        public const string Over15 = OddsMarketKeys.Over15;
        public const string Under15 = OddsMarketKeys.Under15;
        public const string Over35 = OddsMarketKeys.Over35;
        public const string Under35 = OddsMarketKeys.Under35;
    }
}
