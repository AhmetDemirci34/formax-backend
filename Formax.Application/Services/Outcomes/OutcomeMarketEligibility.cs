using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Domain.Constants;

namespace Formax.Application.Services.Outcomes
{
    /// <summary>
    /// MARKET AİLELERİ — uygunluk artık organizasyon başına TEK anahtar değil, organizasyon × market ailesi matrisidir.
    ///
    /// NEDEN: 4.0 ölçümünde (18.09.2026) bir organizasyondaki TEK zayıf market bütün olasılıkları kapatıyordu. Süper Lig'in
    /// 1X2'si tabandan 0,071 nat iyi ve ECE 0,013 iken yalnız KG tabandan kötü olduğu için maçın TAMAMI Limited oluyordu;
    /// Premier League 1X2'de tabandan 0,046 iyiyken yalnız 2.5 Alt/Üst yüzünden kapanıyordu. Yüzde gösteren organizasyon 2/11'di.
    ///
    /// Her aile KENDİ gerçek test performansıyla ölçülür: 2.5 kötü diye 1.5 ya da 3.5 otomatik kapanmaz.
    /// Çifte şans AYRI bir model değildir — 1X2'den matematiksel türetilir ve 1X2'nin uygunluğunu MİRAS ALIR.
    /// </summary>
    public static class MarketFamilies
    {
        public const string MatchResult = "MatchResult1X2";
        public const string DoubleChance = "DoubleChance";
        public const string TotalGoals15 = "TotalGoals15";
        public const string TotalGoals25 = "TotalGoals25";
        public const string TotalGoals35 = "TotalGoals35";
        public const string BothTeamsToScore = "BothTeamsToScore";

        /// <summary>Bağımsız ölçülen aileler (çifte şans hariç — o türetilmiştir).</summary>
        public static readonly IReadOnlyList<string> Measured = new[]
        {
            MatchResult, TotalGoals15, TotalGoals25, TotalGoals35, BothTeamsToScore
        };

        /// <summary>Kullanıcıya gösterilebilen bütün aileler (çifte şans dâhil).</summary>
        public static readonly IReadOnlyList<string> All = new[]
        {
            MatchResult, DoubleChance, TotalGoals15, TotalGoals25, TotalGoals35, BothTeamsToScore
        };

        /// <summary>Snapshot'taki görsel aile (kart grubu) → ölçülen market ailesi eşlemesi.</summary>
        public static string? ForMarketKey(string? marketKey) => marketKey switch
        {
            OddsMarketKeys.Ms1 or OddsMarketKeys.MsX or OddsMarketKeys.Ms2 => MatchResult,
            OddsMarketKeys.DoubleChance1X or OddsMarketKeys.DoubleChanceX2 or OddsMarketKeys.DoubleChance12 => DoubleChance,
            OddsMarketKeys.Over15 or OddsMarketKeys.Under15 => TotalGoals15,
            OddsMarketKeys.Over25 or OddsMarketKeys.Under25 => TotalGoals25,
            OddsMarketKeys.Over35 or OddsMarketKeys.Under35 => TotalGoals35,
            OddsMarketKeys.BttsYes or OddsMarketKeys.BttsNo => BothTeamsToScore,
            _ => null
        };

        public static string Title(string family) => family switch
        {
            MatchResult => "Maç Sonucu",
            DoubleChance => "Çifte Şans",
            TotalGoals15 => "1.5 Gol Çizgisi",
            TotalGoals25 => "2.5 Gol Çizgisi",
            TotalGoals35 => "3.5 Gol Çizgisi",
            BothTeamsToScore => "İki Takımın Gol Durumu",
            _ => family
        };
    }

    /// <summary>Market ailesi yayın durumları — <see cref="MarketEligibilityStatuses.Eligible"/> dışındaki hiçbiri kullanıcıya yüzde taşımaz.</summary>
    public static class MarketEligibilityStatuses
    {
        public const string Eligible = "Eligible";
        public const string Limited = "Limited";
        public const string InsufficientSample = "InsufficientSample";
        public const string WorseThanBaseline = "WorseThanBaseline";
        public const string CalibrationFailed = "CalibrationFailed";
        public const string DataQualityFailed = "DataQualityFailed";
    }

    /// <summary>Maç genelinde yayın durumu.</summary>
    public static class OutcomeOverallStatuses
    {
        /// <summary>En az üç güvenilir kart üretilebiliyor.</summary>
        public const string Full = "Full";
        /// <summary>Bir ya da iki güvenilir kart var.</summary>
        public const string Partial = "Partial";
        /// <summary>Güvenilir kart yok — yüzde gösterilmez.</summary>
        public const string NotEligible = "NotEligible";
    }

    /// <summary>Bir organizasyon × market ailesi hücresinin zamansal test metrikleri ve kararı.</summary>
    public sealed class MarketFamilyMetrics
    {
        public int? LeagueId { get; set; }
        public string Family { get; set; } = string.Empty;
        public int Matches { get; set; }
        /// <summary>Test penceresinde tahmin üretilemeyen maç sayısı (maç düzeyi kapı).</summary>
        public int NotPredicted { get; set; }
        public double DataCoverage { get; set; }
        public double LogLoss { get; set; }
        public double BaselineLogLoss { get; set; }
        public double Brier { get; set; }
        public double BaselineBrier { get; set; }
        public double CalibrationError { get; set; }
        /// <summary>Maç başına (model − taban) log loss farkı; negatif = model iyi.</summary>
        public double LogLossDiff { get; set; }
        public double LogLossDiffCiLow { get; set; }
        public double LogLossDiffCiHigh { get; set; }
        public bool SignificantlyBetter { get; set; }
        /// <summary>Ortalama tahmin − gerçekleşme. 1X2'de en büyük |sapma| (ev/beraberlik/deplasman); ikili markette tek değer.</summary>
        public double MaxBias { get; set; }
        public Dictionary<string, double> Bias { get; set; } = new();
        /// <summary>≥ %65 verilen tahminlerde ortalama olasılık ve gerçekleşme (aşırı güven ölçüsü).</summary>
        public int HighConfidenceCount { get; set; }
        public double HighConfidenceMeanProbability { get; set; }
        public double HighConfidenceHitRate { get; set; }
        /// <summary>≥ %65 verilip tutmayan tahminlerin bütün maçlara oranı.</summary>
        public double OverconfidentWrongRate { get; set; }
        public List<ReliabilityBand> Bands { get; set; } = new();
        public int FinishedLast60Days { get; set; }
        /// <summary>
        /// Lojistik kalibrasyon eğimi/kesişimi (y ~ a + b·logit(p)); 1/0 = kusursuz. YALNIZ BİLGİ: <see cref="MarketEligibilityPolicy.Decide"/>
        /// bu alanları OKUMAZ, kapı eşikleri değişmez. Yayın geçmişine (eligibility-publication) kayıt için ölçülür.
        /// </summary>
        public double? CalibrationSlope { get; set; }
        public double? CalibrationIntercept { get; set; }
        public string Status { get; set; } = MarketEligibilityStatuses.DataQualityFailed;
        public List<string> ReasonCodes { get; set; } = new();

        public bool IsEligible => Status == MarketEligibilityStatuses.Eligible;
    }

    /// <summary>
    /// MARKET UYGUNLUK POLİTİKASI (market-eligibility-1) — eşikler organizasyon politikası <see cref="EligibilityPolicy"/>
    /// ile BİREBİR aynıdır; tek fark, kararın artık her market ailesi için AYRI verilmesidir. Eşikler bu görevde
    /// GEVŞETİLMEDİ: daha çok lig açmak amaç değil, zayıf marketin güçlü marketi kapatmasını durdurmak amaçtır.
    ///
    ///  • InsufficientSample : test maçı &lt; 100
    ///  • WorseThanBaseline  : maç başına log loss farkı &gt; 0 (lig ortalaması tabanından kötü)
    ///  • CalibrationFailed  : ECE &gt; 0,06
    ///  • Limited            : test maçı &lt; 300, ya da tabandan üstünlük %95 eşli aralıkta kanıtlanamıyor,
    ///                         ya da ECE &gt; 0,03, ya da |sapma| &gt; 0,03, ya da son 60 günde &lt; 5 bitmiş maç
    ///  • Eligible           : hiçbiri yok
    /// </summary>
    public static class MarketEligibilityPolicy
    {
        public const string Version = "market-eligibility-1";

        public static void Decide(MarketFamilyMetrics m)
        {
            var reasons = new List<string>();
            if (m.Matches < EligibilityPolicy.DisabledBelowMatches)
            {
                m.Status = MarketEligibilityStatuses.InsufficientSample;
                m.ReasonCodes = new List<string> { "SAMPLE_BELOW_" + EligibilityPolicy.DisabledBelowMatches };
                return;
            }
            if (m.LogLossDiff > 0)
            {
                m.Status = MarketEligibilityStatuses.WorseThanBaseline;
                m.ReasonCodes = new List<string> { "WORSE_THAN_LEAGUE_AVERAGE" };
                return;
            }
            if (m.CalibrationError > EligibilityPolicy.MaxCalibrationErrorLimited)
            {
                m.Status = MarketEligibilityStatuses.CalibrationFailed;
                m.ReasonCodes = new List<string> { "CALIBRATION_ERROR_ABOVE_" + EligibilityPolicy.MaxCalibrationErrorLimited.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) };
                return;
            }
            if (m.Matches < EligibilityPolicy.EnabledMinMatches) reasons.Add("SAMPLE_BELOW_" + EligibilityPolicy.EnabledMinMatches);
            if (!m.SignificantlyBetter) reasons.Add("NOT_SIGNIFICANTLY_BETTER_THAN_BASELINE");
            if (m.CalibrationError > EligibilityPolicy.MaxCalibrationErrorEnabled) reasons.Add("CALIBRATION_ERROR");
            if (Math.Abs(m.MaxBias) > EligibilityPolicy.MaxHomeDrawBias) reasons.Add("SEGMENT_BIAS");
            if (m.FinishedLast60Days < EligibilityPolicy.MinRecentFinished) reasons.Add("STALE_LEAGUE_DATA");
            m.Status = reasons.Count == 0 ? MarketEligibilityStatuses.Eligible : MarketEligibilityStatuses.Limited;
            m.ReasonCodes = reasons;
        }

        /// <summary>Eşik bilgisi — rapor ve testler tek yerden okur.</summary>
        public static string ThresholdSummary =>
            $"min örneklem {EligibilityPolicy.DisabledBelowMatches}/{EligibilityPolicy.EnabledMinMatches}, " +
            $"ECE ≤ {EligibilityPolicy.MaxCalibrationErrorEnabled}/{EligibilityPolicy.MaxCalibrationErrorLimited}, " +
            $"|sapma| ≤ {EligibilityPolicy.MaxHomeDrawBias}, son 60 günde ≥ {EligibilityPolicy.MinRecentFinished} maç";

        /// <summary>
        /// ÇİFTE ŞANS — bağımsız ölçülmez; 1X2'nin uygunluğunu miras alır. P(1X) = P(1) + P(X) olduğu için 1X2 kalibreyse
        /// çifte şans da kalibredir; 1X2 uygun değilse çifte şans da yayımlanamaz.
        /// </summary>
        public static MarketFamilyMetrics InheritDoubleChance(MarketFamilyMetrics result) => new()
        {
            LeagueId = result.LeagueId,
            Family = MarketFamilies.DoubleChance,
            Matches = result.Matches,
            NotPredicted = result.NotPredicted,
            DataCoverage = result.DataCoverage,
            FinishedLast60Days = result.FinishedLast60Days,
            Status = result.Status,
            ReasonCodes = result.Status == MarketEligibilityStatuses.Eligible
                ? new List<string>()
                : new List<string> { "INHERITS_" + MarketFamilies.MatchResult }.Concat(result.ReasonCodes).ToList()
        };
    }

    /// <summary>
    /// MARKET AİLESİ DEĞERLENDİRİCİSİ — aynı kilitli zamansal test örnekleri, her aile için AYRI metrik. Model YENİDEN
    /// EĞİTİLMEZ: örnekler ve dağılımlar 4.0 koşusundan gelir, burada yalnız ölçülür.
    /// </summary>
    public static class MarketFamilyEvaluator
    {
        /// <summary>İkili market: (tahmin, gerçekleşti mi, taban tahmini).</summary>
        private static (double P, bool Y, double B) Binary(string family, ScoreDistribution d, EvalSample s) => family switch
        {
            MarketFamilies.TotalGoals15 => (d.Over(1.5), s.HomeGoals + s.AwayGoals > 1, s.BaseOver15),
            MarketFamilies.TotalGoals25 => (d.Over(2.5), s.HomeGoals + s.AwayGoals > 2, s.BaseOver25),
            MarketFamilies.TotalGoals35 => (d.Over(3.5), s.HomeGoals + s.AwayGoals > 3, s.BaseOver35),
            MarketFamilies.BothTeamsToScore => (d.BttsYes, s.HomeGoals > 0 && s.AwayGoals > 0, s.BaseBtts),
            _ => throw new ArgumentOutOfRangeException(nameof(family), family, "ikili market değil")
        };

        public static MarketFamilyMetrics Evaluate(string family, int? leagueId, IReadOnlyList<EvalSample> samples,
            Func<EvalSample, ScoreDistribution> dist, int notPredicted, int recentFinished, int seed)
        {
            var m = new MarketFamilyMetrics
            {
                LeagueId = leagueId, Family = family, Matches = samples.Count, NotPredicted = notPredicted,
                FinishedLast60Days = recentFinished
            };
            var total = samples.Count + notPredicted;
            m.DataCoverage = total == 0 ? 0 : Math.Round(samples.Count / (double)total, 4);
            if (samples.Count == 0) { MarketEligibilityPolicy.Decide(m); return m; }

            var diffs = new double[samples.Count];
            var pooled = new List<(double P, bool Y)>(samples.Count * 3);
            var high = new List<(double P, bool Y)>();
            double ll = 0, bll = 0, br = 0, bbr = 0;
            var bias = new Dictionary<string, double>();

            if (family == MarketFamilies.MatchResult)
            {
                double ph = 0, pd = 0, pa = 0, ah = 0, ad = 0, aa = 0;
                for (var i = 0; i < samples.Count; i++)
                {
                    var s = samples[i];
                    var d = dist(s);
                    bool hw = s.HomeGoals > s.AwayGoals, dr = s.HomeGoals == s.AwayGoals, aw = s.HomeGoals < s.AwayGoals;
                    var l = GroupEvaluator.ResultLoss(d.HomeWin, d.Draw, d.AwayWin, s.HomeGoals, s.AwayGoals);
                    var lb = GroupEvaluator.ResultLoss(s.BaseHome, s.BaseDraw, s.BaseAway, s.HomeGoals, s.AwayGoals);
                    ll += l; bll += lb; diffs[i] = l - lb;
                    br += Sq(d.HomeWin - B(hw)) + Sq(d.Draw - B(dr)) + Sq(d.AwayWin - B(aw));
                    bbr += Sq(s.BaseHome - B(hw)) + Sq(s.BaseDraw - B(dr)) + Sq(s.BaseAway - B(aw));
                    ph += d.HomeWin; pd += d.Draw; pa += d.AwayWin; ah += B(hw); ad += B(dr); aa += B(aw);
                    pooled.Add((d.HomeWin, hw)); pooled.Add((d.Draw, dr)); pooled.Add((d.AwayWin, aw));
                    var top = Math.Max(d.HomeWin, Math.Max(d.Draw, d.AwayWin));
                    if (top >= 0.65) high.Add((top, top == d.HomeWin ? hw : top == d.AwayWin ? aw : dr));
                }
                var n = (double)samples.Count;
                bias["Home"] = R((ph - ah) / n); bias["Draw"] = R((pd - ad) / n); bias["Away"] = R((pa - aa) / n);
                m.Brier = R(br / n); m.BaselineBrier = R(bbr / n);
            }
            else
            {
                double pp = 0, yy = 0;
                for (var i = 0; i < samples.Count; i++)
                {
                    var s = samples[i];
                    var (p, y, b) = Binary(family, dist(s), s);
                    var l = GroupEvaluator.BinLoss(p, y);
                    var lb = GroupEvaluator.BinLoss(b, y);
                    ll += l; bll += lb; diffs[i] = l - lb;
                    br += Sq(p - B(y)); bbr += Sq(b - B(y));
                    pp += p; yy += B(y);
                    // İkili markette her iki taraf da havuzda: kalibrasyon "Üst" ve "Alt" için birlikte ölçülür.
                    pooled.Add((p, y)); pooled.Add((1 - p, !y));
                    var top = Math.Max(p, 1 - p);
                    if (top >= 0.65) high.Add((top, top == p ? y : !y));
                }
                var n = (double)samples.Count;
                bias["Yes"] = R((pp - yy) / n);
                m.Brier = R(br / n); m.BaselineBrier = R(bbr / n);
            }

            var cnt = (double)samples.Count;
            m.LogLoss = R(ll / cnt); m.BaselineLogLoss = R(bll / cnt);
            m.CalibrationError = R(OutcomeBacktest.Ece(pooled, 10));
            var (slope, intercept) = CalibrationFit(pooled);
            m.CalibrationSlope = slope is double sv ? R(sv) : null;
            m.CalibrationIntercept = intercept is double iv ? R(iv) : null;
            m.Bias = bias;
            m.MaxBias = bias.Count == 0 ? 0 : bias.Values.OrderByDescending(Math.Abs).First();
            m.Bands = OutcomeBacktest.Bands(pooled);
            m.LogLossDiff = R(diffs.Average());
            var (lo, hi) = GroupEvaluator.BootstrapMeanCi(diffs, EligibilityPolicy.BootstrapSamples, seed);
            m.LogLossDiffCiLow = R(lo); m.LogLossDiffCiHigh = R(hi);
            m.SignificantlyBetter = hi < 0;
            m.HighConfidenceCount = high.Count;
            m.HighConfidenceMeanProbability = high.Count == 0 ? 0 : R(high.Average(x => x.P));
            m.HighConfidenceHitRate = high.Count == 0 ? 0 : R(high.Average(x => x.Y ? 1.0 : 0.0));
            m.OverconfidentWrongRate = R(high.Count(x => !x.Y) / cnt);
            MarketEligibilityPolicy.Decide(m);
            return m;
        }

        /// <summary>Bir organizasyonun bütün market ailelerini ölçer; çifte şans 1X2'den miras alır.</summary>
        public static List<MarketFamilyMetrics> EvaluateAll(int? leagueId, IReadOnlyList<EvalSample> samples,
            Func<EvalSample, ScoreDistribution> dist, int notPredicted, int recentFinished, int seed)
        {
            var list = new List<MarketFamilyMetrics>();
            MarketFamilyMetrics? result = null;
            // Tohum aile SIRASINDAN türetilir. string.GetHashCode() .NET'te süreçler arası RASTGELEDİR; bootstrap tohumu ondan
            // türetilirse sınırdaki bir aile (ör. La Liga 1.5 çizgisi, CI üst ucu −0,0002) koşudan koşuya Eligible ↔ Limited
            // arasında gidip gelirdi. Aynı girdi aynı kararı vermek zorundadır.
            for (var i = 0; i < MarketFamilies.Measured.Count; i++)
            {
                var family = MarketFamilies.Measured[i];
                var m = Evaluate(family, leagueId, samples, dist, notPredicted, recentFinished, seed + i * 101);
                if (family == MarketFamilies.MatchResult) result = m;
                list.Add(m);
            }
            if (result != null) list.Insert(1, MarketEligibilityPolicy.InheritDoubleChance(result));
            return list;
        }

        /// <summary>
        /// Lojistik kalibrasyon doğrusu — y ~ a + b·logit(p), Newton–Raphson (deterministik, sabit iterasyon). Tekil ya da
        /// yakınsamayan durumda (null, null) döner; karar kapısı bu değeri kullanmaz.
        /// </summary>
        public static (double? Slope, double? Intercept) CalibrationFit(IReadOnlyList<(double P, bool Y)> xs)
        {
            if (xs.Count < 20) return (null, null);
            double a = 0, b = 1;
            for (var it = 0; it < 50; it++)
            {
                double ga = 0, gb = 0, haa = 0, hab = 0, hbb = 0;
                foreach (var (p, y) in xs)
                {
                    var pc = Math.Clamp(p, 1e-6, 1 - 1e-6);
                    var x = Math.Log(pc / (1 - pc));
                    var q = 1 / (1 + Math.Exp(-(a + b * x)));
                    var r = (y ? 1.0 : 0.0) - q;
                    var w = q * (1 - q);
                    ga += r; gb += r * x;
                    haa += w; hab += w * x; hbb += w * x * x;
                }
                var det = haa * hbb - hab * hab;
                if (!(Math.Abs(det) > 1e-12)) return (null, null);
                var da = (hbb * ga - hab * gb) / det;
                var db = (haa * gb - hab * ga) / det;
                a += da; b += db;
                if (!double.IsFinite(a) || !double.IsFinite(b)) return (null, null);
                if (Math.Abs(da) < 1e-10 && Math.Abs(db) < 1e-10) break;
            }
            return (b, a);
        }

        private static double B(bool v) => v ? 1 : 0;
        private static double Sq(double v) => v * v;
        private static double R(double v) => Math.Round(v, 5);
    }
}
