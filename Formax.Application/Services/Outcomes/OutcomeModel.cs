using System;
using System.Collections.Generic;
using System.Linq;

namespace Formax.Application.Services.Outcomes
{
    /// <summary>Olası sonuç modelinin sürümü — snapshot ve koşu kayıtlarına yazılır.</summary>
    public static class OutcomeModelVersion
    {
        public const string Current = "formax-outcome-2.0";
    }

    /// <summary>Tarihsel (bitmiş) maç — modelin tek girdisi. Maç sonrası başka veri modele girmez.</summary>
    public readonly record struct HistoricalMatch(int MatchId, DateTime KickoffUtc, int LeagueId, int HomeTeamId, int AwayTeamId, int HomeGoals, int AwayGoals);

    /// <summary>
    /// MODEL PARAMETRELERİ — öğrenme oranı eğitim penceresinde, kalibrasyon parametreleri kalibrasyon penceresinde seçilir
    /// (test penceresine bakılarak SEÇİLMEZ).
    /// </summary>
    public sealed class OutcomeModelParameters
    {
        /// <summary>Log-hücum/savunma reytinglerinin maç başına öğrenme oranı (Poisson log-bağ gradyanı).</summary>
        public double LearningRate { get; set; } = 0.05;
        /// <summary>Lig ev/deplasman gol ortalamasının üstel güncelleme oranı.</summary>
        public double LeagueAlpha { get; set; } = 0.02;
        public double DefaultHomeGoals { get; set; } = 1.45;
        public double DefaultAwayGoals { get; set; } = 1.15;
        /// <summary>Kapsamın 1 sayıldığı örneklem (iki takımın daha az maçlısı).</summary>
        public int FullCoverageSample { get; set; } = 12;
        /// <summary>Bu örneklemin altında tahmin ÜRETİLMEZ (yetersiz veri).</summary>
        public int MinSample { get; set; } = 4;
        /// <summary>Son maçı bu kadar günden eskiyse kapsam düşürülür (bayat reyting).</summary>
        public int StaleDays { get; set; } = 150;

        // ── Kalibrasyon (dağılımın kendisine uygulanır: bütün marketler aynı matristen türemeye devam eder) ──
        /// <summary>Beklenen gollerin ölçeği (toplam gol yanlılığını düzeltir).</summary>
        public double GoalScale { get; set; } = 1.0;
        /// <summary>Köşegen (beraberlik) ağırlığı; bağımsız Poisson'un beraberlik yanlılığını düzeltir.</summary>
        public double DrawInflation { get; set; } = 1.0;
        /// <summary>Lig taban dağılımıyla sabit karışım (aşırı güveni tavlar).</summary>
        public double BaselineMix { get; set; } = 0.0;
        /// <summary>Veri kapsamı düştükçe taban dağılıma eklenen karışım (belirsizlik artar).</summary>
        public double UncertaintyMix { get; set; } = 0.35;
        /// <summary>Lig başına gol ölçeği (küçük örneklemde global değere daraltılmış).</summary>
        public Dictionary<int, double> LeagueGoalScale { get; set; } = new();

        public OutcomeModelParameters Clone() => new()
        {
            LearningRate = LearningRate, LeagueAlpha = LeagueAlpha, DefaultHomeGoals = DefaultHomeGoals, DefaultAwayGoals = DefaultAwayGoals,
            FullCoverageSample = FullCoverageSample, MinSample = MinSample, StaleDays = StaleDays, GoalScale = GoalScale,
            DrawInflation = DrawInflation, BaselineMix = BaselineMix, UncertaintyMix = UncertaintyMix,
            LeagueGoalScale = new Dictionary<int, double>(LeagueGoalScale)
        };
    }

    /// <summary>Takım reytingi — yalnız geçmiş maçlardan, sırayla güncellenir (zamansal sızıntı yok).</summary>
    public sealed class TeamRating
    {
        public double LogAttack;
        public double LogDefence;
        public int Matches;
        public DateTime? LastMatchUtc;
        /// <summary>Son 10 maç (attığı, yediği, iç saha mı).</summary>
        public readonly Queue<(int For, int Against, bool Home)> Recent = new();
    }

    public sealed class LeagueGoalState
    {
        public double Home;
        public double Away;
        public int Matches;
    }

    /// <summary>Tek maç için modelin girdileri (sonuç bilinmeden hesaplanır).</summary>
    public sealed record OutcomeExpectation(
        double LambdaHome, double LambdaAway, double LeagueHome, double LeagueAway,
        int HomeSample, int AwaySample, double Coverage, bool Sufficient,
        double HomeRecentFor, double HomeRecentAgainst, double AwayRecentFor, double AwayRecentAgainst, int HomeRecentCount, int AwayRecentCount,
        DateTime? HomeLastMatchUtc, DateTime? AwayLastMatchUtc);

    /// <summary>
    /// ONLINE POISSON REYTİNG MODELİ — her takım için log-hücum ve log-savunma; λ_ev = lig_ev × e^(hücum_ev + savunma_dep),
    /// λ_dep = lig_dep × e^(hücum_dep + savunma_ev). Her maçtan SONRA Poisson log-olabilirlik gradyanıyla güncellenir; bir maçın
    /// tahmini yalnız o maçtan önce bitmiş maçları görür. Rakip gücü doğal olarak hesaba girer (beklenen gol rakibin savunmasını içerir).
    /// </summary>
    public sealed class OutcomeRatingModel
    {
        private readonly Dictionary<int, TeamRating> _teams = new();
        private readonly Dictionary<int, LeagueGoalState> _leagues = new();
        public OutcomeModelParameters Parameters { get; }
        public DateTime? LastUpdateUtc { get; private set; }
        public int ProcessedMatches { get; private set; }

        public OutcomeRatingModel(OutcomeModelParameters parameters) => Parameters = parameters;

        private TeamRating Team(int id) => _teams.TryGetValue(id, out var t) ? t : _teams[id] = new TeamRating();

        private LeagueGoalState League(int id)
            => _leagues.TryGetValue(id, out var l) ? l : _leagues[id] = new LeagueGoalState { Home = Parameters.DefaultHomeGoals, Away = Parameters.DefaultAwayGoals };

        public OutcomeExpectation Expect(int leagueId, int homeTeamId, int awayTeamId, DateTime kickoffUtc)
        {
            var h = _teams.GetValueOrDefault(homeTeamId);
            var a = _teams.GetValueOrDefault(awayTeamId);
            var lg = _leagues.GetValueOrDefault(leagueId) ?? new LeagueGoalState { Home = Parameters.DefaultHomeGoals, Away = Parameters.DefaultAwayGoals };
            var lh = Clamp(lg.Home * Math.Exp((h?.LogAttack ?? 0) + (a?.LogDefence ?? 0)), 0.15, 4.5);
            var la = Clamp(lg.Away * Math.Exp((a?.LogAttack ?? 0) + (h?.LogDefence ?? 0)), 0.15, 4.5);
            var nh = h?.Matches ?? 0;
            var na = a?.Matches ?? 0;
            var coverage = Math.Clamp(Math.Min(nh, na) / (double)Parameters.FullCoverageSample, 0, 1);
            if (IsStale(h, kickoffUtc) || IsStale(a, kickoffUtc)) coverage *= 0.6;
            var (hf, hg, hc) = RecentAverages(h);
            var (af, ag, ac) = RecentAverages(a);
            return new OutcomeExpectation(lh, la, lg.Home, lg.Away, nh, na, coverage, Math.Min(nh, na) >= Parameters.MinSample,
                hf, hg, af, ag, hc, ac, h?.LastMatchUtc, a?.LastMatchUtc);
        }

        private bool IsStale(TeamRating? t, DateTime kickoffUtc)
            => t?.LastMatchUtc is DateTime last && (kickoffUtc - last).TotalDays > Parameters.StaleDays;

        private static (double For, double Against, int Count) RecentAverages(TeamRating? t)
            => t == null || t.Recent.Count == 0 ? (0, 0, 0) : (t.Recent.Average(r => r.For), t.Recent.Average(r => r.Against), t.Recent.Count);

        /// <summary>Bitmiş maçı modele işler (tahmin YAPILDIKTAN sonra çağrılır).</summary>
        public void Update(HistoricalMatch m)
        {
            var h = Team(m.HomeTeamId);
            var a = Team(m.AwayTeamId);
            var lg = League(m.LeagueId);
            var lh = Clamp(lg.Home * Math.Exp(h.LogAttack + a.LogDefence), 0.15, 4.5);
            var la = Clamp(lg.Away * Math.Exp(a.LogAttack + h.LogDefence), 0.15, 4.5);
            var eta = Parameters.LearningRate;
            var errH = Math.Clamp(m.HomeGoals - lh, -4, 4);
            var errA = Math.Clamp(m.AwayGoals - la, -4, 4);
            h.LogAttack = Clamp(h.LogAttack + eta * errH, -1.6, 1.6);
            a.LogDefence = Clamp(a.LogDefence + eta * errH, -1.6, 1.6);
            a.LogAttack = Clamp(a.LogAttack + eta * errA, -1.6, 1.6);
            h.LogDefence = Clamp(h.LogDefence + eta * errA, -1.6, 1.6);
            var alpha = lg.Matches < 50 ? Math.Max(Parameters.LeagueAlpha, 1.0 / (lg.Matches + 2)) : Parameters.LeagueAlpha;
            lg.Home += alpha * (m.HomeGoals - lg.Home);
            lg.Away += alpha * (m.AwayGoals - lg.Away);
            lg.Matches++;
            Push(h, m.HomeGoals, m.AwayGoals, true, m.KickoffUtc);
            Push(a, m.AwayGoals, m.HomeGoals, false, m.KickoffUtc);
            LastUpdateUtc = m.KickoffUtc;
            ProcessedMatches++;
        }

        private static void Push(TeamRating t, int gf, int ga, bool home, DateTime at)
        {
            t.Matches++;
            t.LastMatchUtc = at;
            t.Recent.Enqueue((gf, ga, home));
            while (t.Recent.Count > 10) t.Recent.Dequeue();
        }

        private static double Clamp(double v, double lo, double hi) => Math.Max(lo, Math.Min(hi, v));
    }

    /// <summary>
    /// TUTARLI SKOR DAĞILIMI — P(Ev = h, Dep = a), 0..10 gol. Bütün marketler (1X2, çifte şans, alt/üst, KG, takım golü,
    /// temiz kale, gol aralığı, en olası skorlar) YALNIZ bu matristen türetilir; toplamlar yapısal olarak tutarlıdır.
    /// </summary>
    public sealed class ScoreDistribution
    {
        public const int MaxGoals = 10;
        private readonly double[,] _p = new double[MaxGoals + 1, MaxGoals + 1];

        private ScoreDistribution() { }

        public double this[int h, int a] => _p[h, a];

        public static ScoreDistribution Poisson(double lambdaHome, double lambdaAway, double drawInflation = 1.0)
        {
            var d = new ScoreDistribution();
            var hp = Pmf(lambdaHome);
            var ap = Pmf(lambdaAway);
            double total = 0;
            for (var i = 0; i <= MaxGoals; i++)
                for (var j = 0; j <= MaxGoals; j++)
                {
                    var v = hp[i] * ap[j] * (i == j ? drawInflation : 1.0);
                    d._p[i, j] = v;
                    total += v;
                }
            d.Normalize(total);
            return d;
        }

        /// <summary>İki dağılımın karışımı: (1−w)·this + w·other.</summary>
        public ScoreDistribution Mix(ScoreDistribution other, double w)
        {
            w = Math.Clamp(w, 0, 1);
            var d = new ScoreDistribution();
            for (var i = 0; i <= MaxGoals; i++)
                for (var j = 0; j <= MaxGoals; j++)
                    d._p[i, j] = (1 - w) * _p[i, j] + w * other._p[i, j];
            return d;
        }

        private void Normalize(double total)
        {
            if (total <= 0) return;
            for (var i = 0; i <= MaxGoals; i++)
                for (var j = 0; j <= MaxGoals; j++)
                    _p[i, j] /= total;
        }

        private static double[] Pmf(double lambda)
        {
            lambda = Math.Max(0.01, lambda);
            var p = new double[MaxGoals + 1];
            p[0] = Math.Exp(-lambda);
            for (var k = 1; k <= MaxGoals; k++) p[k] = p[k - 1] * lambda / k;
            return p;
        }

        private double Sum(Func<int, int, bool> pred)
        {
            double s = 0;
            for (var i = 0; i <= MaxGoals; i++)
                for (var j = 0; j <= MaxGoals; j++)
                    if (pred(i, j)) s += _p[i, j];
            return s;
        }

        public double Total => Sum((_, _) => true);
        public double HomeWin => Sum((h, a) => h > a);
        public double Draw => Sum((h, a) => h == a);
        public double AwayWin => Sum((h, a) => h < a);
        public double Over(double line) => Sum((h, a) => h + a > line);
        public double Under(double line) => Sum((h, a) => h + a < line);
        public double BttsYes => Sum((h, a) => h > 0 && a > 0);
        public double BttsNo => Sum((h, a) => h == 0 || a == 0);
        public double HomeScores => Sum((h, _) => h > 0);
        public double AwayScores => Sum((_, a) => a > 0);
        public double HomeCleanSheet => Sum((_, a) => a == 0);
        public double AwayCleanSheet => Sum((h, _) => h == 0);
        public double TotalBetween(int min, int max) => Sum((h, a) => h + a >= min && h + a <= max);
        public double ExpectedTotalGoals => ExpectedHome + ExpectedAway;
        public double ExpectedHome { get { double s = 0; for (var i = 0; i <= MaxGoals; i++) for (var j = 0; j <= MaxGoals; j++) s += i * _p[i, j]; return s; } }
        public double ExpectedAway { get { double s = 0; for (var i = 0; i <= MaxGoals; i++) for (var j = 0; j <= MaxGoals; j++) s += j * _p[i, j]; return s; } }

        public IReadOnlyList<(int Home, int Away, double P)> TopScores(int count)
        {
            var list = new List<(int, int, double)>();
            for (var i = 0; i <= MaxGoals; i++)
                for (var j = 0; j <= MaxGoals; j++)
                    list.Add((i, j, _p[i, j]));
            return list.OrderByDescending(x => x.Item3).ThenBy(x => x.Item1 + x.Item2).Take(count).ToList();
        }
    }

    /// <summary>Tek maç tahmini — ham ve kalibre dağılım + taban dağılım.</summary>
    public sealed record OutcomePrediction(
        OutcomeExpectation Expectation, ScoreDistribution Raw, ScoreDistribution Calibrated, ScoreDistribution Baseline, double UncertaintyWeight);

    /// <summary>Beklenti → dağılımlar. Kalibrasyon parametreleri dağılımın kendisine uygulanır.</summary>
    public static class OutcomePredictor
    {
        public static OutcomePrediction Predict(OutcomeExpectation e, int leagueId, OutcomeModelParameters p)
        {
            var raw = ScoreDistribution.Poisson(e.LambdaHome, e.LambdaAway);
            var scale = p.LeagueGoalScale.TryGetValue(leagueId, out var ls) ? ls : p.GoalScale;
            var baseline = ScoreDistribution.Poisson(e.LeagueHome * scale, e.LeagueAway * scale, p.DrawInflation);
            var model = ScoreDistribution.Poisson(e.LambdaHome * scale, e.LambdaAway * scale, p.DrawInflation);
            var w = Math.Clamp(p.BaselineMix + p.UncertaintyMix * (1 - e.Coverage), 0, 0.85);
            return new OutcomePrediction(e, raw, model.Mix(baseline, w), baseline, w);
        }
    }
}
