using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Formax.Application.Services.Outcomes
{
    /// <summary>
    /// MODEL 6 GÖLGE (formax-outcome-6-shadow) — 30.09.2026'da DONDURULDU; yalnız 1X2. Kullanıcıya gösterilmez; yalnız ileriye dönük
    /// kilitli kayıt ve puanlama içindir. Parametreler gelecekte sonuç görüldükçe DEĞİŞMEZ (takım güçleri ve lig ν/iç saha değerleri
    /// motorun tanımı gereği çevrimiçi ilerler; yapı, sabitler ve ağırlıklar sabittir).
    ///
    /// Tanım: 4.0 kalibre 1X2 ile Davidson beraberlikli Elo'nun 1X2'si doğrusal havuzlanır (yerel lig w = 0,485; Şampiyonlar Ligi ve
    /// Konferans Ligi w = 0,049; Avrupa Ligi uygulanmaz → 4.0). Ağırlıklar ve Davidson yapısı 26.09 kesimine kadarki iç pencerede
    /// ([2024-09-05, 2026-09-25), 6.490 kilitli maç) nested prosedürle seçildi; kalibrasyon katmanı iç pencerede kazanç göstermediği için yok.
    ///
    /// Tarihsel kanıt (geriye dönük; final test daha önce görülmüştü): 6 dış fold'da 5.317 maç, aynı aile (fold başına iç seçim) +
    /// kullanıcı org kuralı (Avrupa Ligi +0,0103 → uygulanmaz): 1X2 log loss 1,00678 → 1,00327, eşli blok %95 [−0,00686, −0,00046],
    /// 5/6 fold, Brier 0,60164 → 0,59932, ECE 0,00456 → 0,00880, doğruluk %50,84 → %51,25, 9/11 organizasyon kötüleşmedi.
    /// Üretim kararı YALNIZ ileriye dönük kapıdan (<see cref="ForwardGate"/>) gelir.
    /// </summary>
    public static class Model6Shadow
    {
        public const string Version = "formax-outcome-6-shadow";
        public const string Method = "nested-wf-6fold-lag120-1";

        public static readonly DavidsonEloConfig Davidson = new() { K = 16, SeasonCarry = 1.0, PromotionPrior = 60 };
        public const double DomesticWeight = 0.485;
        public const double UefaWeight = 0.049;
        public static readonly HashSet<int> Uefa = new() { 2, 3, 848 };
        /// <summary>Tarihsel kapıda +0,01 ya da daha fazla kötüleşen organizasyon → Model 4 aynen (UEFA Avrupa Ligi).</summary>
        public static readonly HashSet<int> NotApplied = new() { 3 };

        public static string ConfigHash { get; } = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|",
            Version, Method, OutcomeModelVersion.Current, Davidson.Canonical, DomesticWeight.ToString("R", CultureInfo.InvariantCulture),
            UefaWeight.ToString("R", CultureInfo.InvariantCulture), string.Join(",", Uefa.OrderBy(x => x)), string.Join(",", NotApplied.OrderBy(x => x)),
            OutcomeBacktest.MethodVersion)))).ToLowerInvariant()[..32];

        public static bool Applies(int leagueId) => !NotApplied.Contains(leagueId);

        public static double WeightFor(int leagueId) => !Applies(leagueId) ? 0 : Uefa.Contains(leagueId) ? UefaWeight : DomesticWeight;

        /// <summary>Model 6 1X2 — 4.0 kalibre 1X2 ve Davidson 1X2'nin havuzu. Uygulanmayan organizasyonda 4.0 aynen döner.</summary>
        public static Probs3 Predict(Probs3 model40, int leagueId, Probs3 davidson)
        {
            var w = WeightFor(leagueId);
            return w == 0 ? model40 : Model6Lab.Pool(model40, new[] { (davidson, w) });
        }

        /// <summary>Kayıt için skor matrisi: 4.0 matrisinin sonuç sınıfları Model 6 1X2'sine yeniden ağırlıklandırılır (yalnız 1X2 kaydedilir).</summary>
        public static ScoreDistribution Distribution(ScoreDistribution model40, int leagueId, Probs3 davidson)
        {
            if (!Applies(leagueId)) return model40;
            var p = Predict(Probs3.From(model40), leagueId, davidson);
            return model40.ReweightResult(p.H, p.D, p.A);
        }

        // ═══════════════════════ İLERİYE DÖNÜK ÜRETİM KAPISI (K) ═══════════════════════

        public const int MinForwardMatches = 300;
        public const int MinForwardWeeks = 5;
        public const int MinPerOrganization = 30;

        /// <summary>Aynı maç için kickoff öncesi kilitlenmiş ve puanlanmış 4.0 ve Model 6 1X2 kaydı.</summary>
        public sealed record ForwardPair(int MatchId, int LeagueId, DateTime KickoffUtc, Probs3 Model40, Probs3 Model6, int Outcome);

        public sealed record ForwardGateResult(string Status, int Matches, int Weeks, IReadOnlyDictionary<int, int> PerOrganization, double LogLoss40, double LogLoss6,
            (double Mean, double Low, double High) LogLossDiff, double Brier40, double Brier6, double Ece40, double Ece6, double Accuracy40, double Accuracy6,
            double[] Recall40, double[] Recall6, IReadOnlyList<string> Reasons);

        /// <summary>
        /// Model 6 ancak şu yeni ve görülmemiş sonuçlarla üretim adayı olur: ≥ 300 puanlanmış maç, ≥ 5 farklı maç haftası, uygulanacağı her
        /// organizasyonda ≥ 30 maç, eşli log loss farkının blok %95 üst sınırı &lt; 0, Brier / ECE / doğruluk kötüleşmemiş, hiçbir sınıfın
        /// recall'u 0,02'den fazla düşmemiş. Bu değerlendirme ÜRETİMİ DEĞİŞTİRMEZ — yalnız rapor.
        /// </summary>
        public static ForwardGateResult ForwardGate(IReadOnlyList<ForwardPair> pairs)
        {
            var reasons = new List<string>();
            var weeks = pairs.Select(p => ISOWeek.GetYear(p.KickoffUtc) * 100 + ISOWeek.GetWeekOfYear(p.KickoffUtc)).Distinct().Count();
            var perOrg = pairs.Where(p => Applies(p.LeagueId)).GroupBy(p => p.LeagueId).ToDictionary(g => g.Key, g => g.Count());
            var a = Model6Lab.Evaluate(pairs.Select(p => (p.Model40, p.Outcome)).ToList());
            var b = Model6Lab.Evaluate(pairs.Select(p => (p.Model6, p.Outcome)).ToList());
            var diff = OutcomeAccuracyLab.PairedBlockBootstrap(pairs.Select(p => (p.KickoffUtc, Model6Lab.LogLoss(p.Model6, p.Outcome) - Model6Lab.LogLoss(p.Model40, p.Outcome))).ToList());
            if (pairs.Count < MinForwardMatches) reasons.Add($"INSUFFICIENT_MATCHES {pairs.Count}/{MinForwardMatches}");
            if (weeks < MinForwardWeeks) reasons.Add($"INSUFFICIENT_WEEKS {weeks}/{MinForwardWeeks}");
            foreach (var kv in perOrg.Where(kv => kv.Value < MinPerOrganization)) reasons.Add($"ORG_SAMPLE {kv.Key}:{kv.Value}/{MinPerOrganization}");
            if (pairs.Count > 0)
            {
                if (!(diff.High < 0)) reasons.Add("LOGLOSS_NOT_SIGNIFICANT");
                if (b.Brier > a.Brier) reasons.Add("BRIER_WORSE");
                if (b.Ece > a.Ece) reasons.Add("ECE_WORSE");
                if (b.Accuracy < a.Accuracy) reasons.Add("ACCURACY_WORSE");
                for (var c = 0; c < 3; c++)
                    if (!double.IsNaN(a.Recall[c]) && b.Recall[c] < a.Recall[c] - 0.02) reasons.Add("CLASS_DEGRADED " + Model6Lab.ClassNames[c]);
            }
            var sampleBlocked = pairs.Count < MinForwardMatches || weeks < MinForwardWeeks || perOrg.Values.Any(v => v < MinPerOrganization) || perOrg.Count == 0;
            var status = sampleBlocked ? "BLOCKED_INSUFFICIENT_FORWARD_DATA" : reasons.Count == 0 ? "PRODUCTION_CANDIDATE" : "FAILED";
            return new ForwardGateResult(status, pairs.Count, weeks, perOrg, a.LogLoss, b.LogLoss, diff, a.Brier, b.Brier, a.Ece, b.Ece, a.Accuracy, b.Accuracy,
                a.Recall, b.Recall, reasons);
        }
    }
}
