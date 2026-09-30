using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Formax.Application.Services.Outcomes
{
    /// <summary>1X2 olasılık üçlüsü (ev, beraberlik, deplasman). Toplamı 1'dir.</summary>
    public readonly record struct Probs3(double H, double D, double A)
    {
        public static Probs3 Normalized(double h, double d, double a)
        {
            h = Math.Max(1e-9, h); d = Math.Max(1e-9, d); a = Math.Max(1e-9, a);
            var z = h + d + a;
            return new Probs3(h / z, d / z, a / z);
        }

        public static Probs3 From(ScoreDistribution d) => Normalized(d.HomeWin, d.Draw, d.AwayWin);

        public double this[int c] => c == 0 ? H : c == 1 ? D : A;

        /// <summary>En yüksek olasılıklı sınıf (0 ev, 1 beraberlik, 2 deplasman). Eşitlikte ev → deplasman → beraberlik sırası.</summary>
        public int Argmax => H >= A && H >= D ? 0 : A >= D ? 2 : 1;

        public bool IsValid => double.IsFinite(H) && double.IsFinite(D) && double.IsFinite(A) && H >= 0 && D >= 0 && A >= 0 && Math.Abs(H + D + A - 1) < 1e-6;
    }

    /// <summary>
    /// DAVIDSON BERABERLİKLİ ELO (Model 6 aday ailesi 1, 30.09.2026) — sonuç tabanlı dinamik takım gücü, lig bazlı iç saha avantajı ve
    /// ayrı BERABERLİK parametresi ν:
    ///   x = (R_ev − R_dep + EV_lig)·ln10/400 [+ form + dinlenme],  Z = e^{x/2} + e^{−x/2} + ν_lig
    ///   P(ev) = e^{x/2}/Z,  P(beraberlik) = ν_lig/Z,  P(dep) = e^{−x/2}/Z.
    /// Sıralı logitten farkı: beraberlik olasılığı güç farkından ayrı bir parametreyle taşınır (dengeli maçta ν/(2+ν)).
    ///
    /// ZAMAN KURALI: bir maçın sonucu ancak başlama + <see cref="DavidsonEloConfig.ResultLagMinutes"/> dakikada bilinir
    /// (özellik.AvailableAt &lt; maç.Kickoff). Aynı saatte başlayan ya da henüz bitmemiş maçların sonucu hiçbir tahmine girmez.
    /// Lig ν ve iç saha sapmaları her güncellemede global değere çekilir (küçük örneklem shrinkage).
    /// </summary>
    public sealed record DavidsonEloConfig
    {
        /// <summary>Takım gücü adımı (Elo puanı; gol farkı çarpanıyla).</summary>
        public double K { get; init; } = 20;
        /// <summary>Uzun aradan (sezon arası) sonra lig ortalamasından sapmanın korunan payı (1 = sıfırlama yok).</summary>
        public double SeasonCarry { get; init; } = 1.0;
        public int SeasonBreakDays { get; init; } = 50;
        /// <summary>Lig değiştiren takımın kontrollü önseli (Elo puanı; 0 = kapalı). Güçlü lige geçen ≤ ortalama − P, zayıfa geçen ≥ ortalama + P.</summary>
        public double PromotionPrior { get; init; }
        /// <summary>Rakip düzeltilmiş form ağırlığı (logit birimi; 0 = kapalı). Form = sonuç − Elo beklentisi artıklarının ortalaması.</summary>
        public double FormWeight { get; init; }
        /// <summary>Formun maç cinsinden yarı ömrü (<see cref="FormRecency"/> kapalıysa son 10 maçın düz ortalaması).</summary>
        public double FormHalfLife { get; init; } = 5;
        public bool FormRecency { get; init; } = true;
        /// <summary>Dinlenme farkı etkisi (Elo puanı / gün; 0 = kapalı).</summary>
        public double RestBeta { get; init; }
        /// <summary>Lig bazlı beraberlik parametresi (false = yalnız global ν).</summary>
        public bool LeagueDraw { get; init; } = true;
        /// <summary>İç saha avantajı (false = 0).</summary>
        public bool HomeAdvantage { get; init; } = true;
        /// <summary>Lig gücü önseli: lige yeni giren takım lig ortalamasından başlar (false = 1500).</summary>
        public bool LeagueMeanPrior { get; init; } = true;
        /// <summary>Gol farkı çarpanının güvenli tavanı.</summary>
        public int GoalDiffCap { get; init; } = 3;
        /// <summary>Sonucun bilinir kabul edildiği an: başlama + bu kadar dakika.</summary>
        public int ResultLagMinutes { get; init; } = 120;

        public const double InitialHomeAdvantage = 60;
        /// <summary>Dengeli maçta ~%26 beraberlik (ν/(2+ν)). Yalnız başlangıç; veriyle öğrenilir.</summary>
        public const double InitialLogNu = -0.353;
        public const double HomeGlobalRate = 1.0, HomeLeagueRate = 4.0, HomeLeagueDecay = 0.01;
        public const double NuGlobalRate = 0.004, NuLeagueRate = 0.02, NuLeagueDecay = 0.005;

        public string Canonical => string.Join(";", new[]
        {
            "davidson-elo-1", F(K), F(SeasonCarry), SeasonBreakDays.ToString(CultureInfo.InvariantCulture), F(PromotionPrior), F(FormWeight), F(FormHalfLife),
            FormRecency ? "rec" : "flat", F(RestBeta), LeagueDraw ? "lnu" : "gnu", HomeAdvantage ? "ha" : "noha", LeagueMeanPrior ? "lmp" : "flat1500",
            GoalDiffCap.ToString(CultureInfo.InvariantCulture), ResultLagMinutes.ToString(CultureInfo.InvariantCulture),
            F(InitialHomeAdvantage), F(InitialLogNu), F(HomeGlobalRate), F(HomeLeagueRate), F(HomeLeagueDecay), F(NuGlobalRate), F(NuLeagueRate), F(NuLeagueDecay)
        });

        public string Hash => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Canonical))).ToLowerInvariant()[..16];

        private static string F(double v) => v.ToString("R", CultureInfo.InvariantCulture);
    }

    public sealed class DavidsonElo
    {
        private sealed class Team
        {
            public double R = 1500;
            public bool Known;
            public int? HomeLeague;
            public DateTime? Last;
            public double Form;
            public readonly Queue<double> Residuals = new();
        }

        private sealed record Pending(HistoricalMatch Match, double X, Probs3 P, double HomeR, double AwayR, int? HomeLeagueAfter, int? AwayLeagueAfter);

        private readonly DavidsonEloConfig _c;
        private readonly CompetitionCatalog _catalog;
        private readonly Dictionary<int, Team> _teams = new();
        private readonly Dictionary<int, HashSet<int>> _members = new();
        private readonly Dictionary<int, double> _haOff = new();
        private readonly Dictionary<int, double> _nuOff = new();
        private readonly Queue<Pending> _pending = new();
        private double _haGlobal = DavidsonEloConfig.InitialHomeAdvantage;
        private double _logNuGlobal = DavidsonEloConfig.InitialLogNu;

        public DavidsonElo(DavidsonEloConfig config, CompetitionCatalog catalog)
        {
            _c = config; _catalog = catalog;
        }

        public DavidsonEloConfig Config => _c;
        public int Applied { get; private set; }

        /// <summary>
        /// Geçmişi zaman sırasıyla işler: her maç için ÖNCE tahmin (<paramref name="onPredict"/>), sonuç ancak başlama + gecikme
        /// geçtikten sonra modele girer. <paramref name="toUtc"/>'dan önce bilinir hâle gelen bütün sonuçlar uygulanmış olarak döner.
        /// </summary>
        public static DavidsonElo Replay(IReadOnlyList<HistoricalMatch> ordered, CompetitionCatalog catalog, DavidsonEloConfig config, DateTime toUtc,
            Action<HistoricalMatch, Probs3>? onPredict = null)
        {
            var e = new DavidsonElo(config, catalog);
            foreach (var m in ordered)
            {
                if (m.KickoffUtc >= toUtc) break;
                if (catalog.IsExcluded(m.LeagueId)) continue;
                e.AdvanceTo(m.KickoffUtc);
                var p = e.PredictAndQueue(m);
                onPredict?.Invoke(m, p);
            }
            e.AdvanceTo(toUtc);
            return e;
        }

        /// <summary>Sonucu <paramref name="t"/>'den ÖNCE bilinen (başlama + gecikme &lt; t) bütün maçları uygular.</summary>
        public void AdvanceTo(DateTime t)
        {
            var lag = TimeSpan.FromMinutes(_c.ResultLagMinutes);
            while (_pending.Count > 0 && _pending.Peek().Match.KickoffUtc + lag < t) Apply(_pending.Dequeue());
        }

        /// <summary>Geleceğe dönük tahmin — durumu değiştirmez.</summary>
        public Probs3 Predict(int leagueId, int homeTeamId, int awayTeamId, DateTime kickoffUtc)
            => Compute(leagueId, homeTeamId, awayTeamId, kickoffUtc).P;

        private Probs3 PredictAndQueue(HistoricalMatch m)
        {
            var c = Compute(m.LeagueId, m.HomeTeamId, m.AwayTeamId, m.KickoffUtc);
            _pending.Enqueue(new Pending(m, c.X, c.P, c.Rh, c.Ra, c.Hl, c.Al));
            return c.P;
        }

        private (Probs3 P, double X, double Rh, double Ra, int? Hl, int? Al) Compute(int leagueId, int homeId, int awayId, DateTime t)
        {
            var isLeague = _catalog.IsLeague(leagueId);
            var (rh, hl) = Effective(homeId, leagueId, isLeague, t);
            var (ra, al) = Effective(awayId, leagueId, isLeague, t);
            var ha = _c.HomeAdvantage ? _haGlobal + _haOff.GetValueOrDefault(leagueId) : 0;
            var x = (rh - ra + ha) * Math.Log(10) / 400;
            if (_c.FormWeight != 0) x += _c.FormWeight * (FormOf(homeId) - FormOf(awayId));
            if (_c.RestBeta != 0) x += _c.RestBeta * (Rest(homeId, t) - Rest(awayId, t)) * Math.Log(10) / 400;
            var nu = Math.Exp(_logNuGlobal + (_c.LeagueDraw ? _nuOff.GetValueOrDefault(leagueId) : 0));
            return (Davidson(x, nu), x, rh, ra, hl, al);
        }

        /// <summary>Davidson beraberlik modeli: logit farkı x ve beraberlik parametresi ν'den 1X2.</summary>
        public static Probs3 Davidson(double x, double nu)
        {
            x = Math.Clamp(x, -30, 30);
            var h = Math.Exp(x / 2); var a = Math.Exp(-x / 2);
            var z = h + a + nu;
            return new Probs3(h / z, nu / z, a / z);
        }

        private double FormOf(int team)
        {
            if (!_teams.TryGetValue(team, out var t)) return 0;
            return _c.FormRecency ? t.Form : t.Residuals.Count == 0 ? 0 : t.Residuals.Average();
        }

        private double Rest(int team, DateTime at)
            => _teams.TryGetValue(team, out var t) && t.Last is DateTime l ? Math.Clamp((at - l).TotalDays, 2, 8) : 5;

        /// <summary>Tahmin anındaki etkin güç (sezon arası daraltma + lig değişimi önseli sanal uygulanır) ve maç sonrası ev ligi.</summary>
        private (double R, int? HomeLeague) Effective(int teamId, int leagueId, bool isLeague, DateTime at)
        {
            var t = _teams.GetValueOrDefault(teamId);
            var newLeague = isLeague ? leagueId : t?.HomeLeague;
            if (t == null || !t.Known)
            {
                if (_c.LeagueMeanPrior && isLeague && LeagueMean(leagueId, at) is double mu)
                    return (mu - (_c.PromotionPrior > 0 ? _c.PromotionPrior : 0), newLeague);
                return (1500, newLeague);
            }
            var r = t.R;
            if (t.Last is DateTime last && (at - last).TotalDays > _c.SeasonBreakDays && _c.SeasonCarry < 1 && t.HomeLeague is int own
                && LeagueMean(own, at, exclude: teamId) is double m0)
                r = m0 + _c.SeasonCarry * (r - m0);
            if (isLeague && t.HomeLeague is int old && old != leagueId && _c.PromotionPrior > 0
                && LeagueMean(leagueId, at, exclude: teamId) is double mNew && LeagueMean(old, at, exclude: teamId) is double mOld)
                r = mNew >= mOld ? Math.Min(r, mNew - _c.PromotionPrior) : Math.Max(r, mNew + _c.PromotionPrior);
            return (r, newLeague);
        }

        /// <summary>Ligin aktif (son 365 gün) üyelerinin ortalama gücü; 5'ten az üye varsa tanımsız.</summary>
        private double? LeagueMean(int leagueId, DateTime at, int exclude = -1)
        {
            if (!_members.TryGetValue(leagueId, out var set)) return null;
            double s = 0; var n = 0;
            foreach (var id in set)
            {
                if (id == exclude) continue;
                var t = _teams[id];
                if (t.HomeLeague != leagueId || t.Last is not DateTime l || (at - l).TotalDays > 365) continue;
                s += t.R; n++;
            }
            return n < 5 ? null : s / n;
        }

        private void Apply(Pending p)
        {
            var m = p.Match;
            var h = TeamOf(m.HomeTeamId); var a = TeamOf(m.AwayTeamId);
            h.R = p.HomeR; a.R = p.AwayR; h.Known = true; a.Known = true;
            SetLeague(m.HomeTeamId, h, p.HomeLeagueAfter);
            SetLeague(m.AwayTeamId, a, p.AwayLeagueAfter);
            double yh = m.HomeGoals > m.AwayGoals ? 1 : 0, yd = m.HomeGoals == m.AwayGoals ? 1 : 0;
            var s = yh + 0.5 * yd;
            var e = p.P.H + 0.5 * p.P.D;
            var g = s - e; // Davidson log-olabilirliğinin x'e göre gradyanı
            var mult = 1 + Math.Log(1 + Math.Min(Math.Abs(m.HomeGoals - m.AwayGoals), _c.GoalDiffCap));
            h.R += _c.K * mult * g;
            a.R -= _c.K * mult * g;
            if (_c.HomeAdvantage)
            {
                _haGlobal += DavidsonEloConfig.HomeGlobalRate * g;
                _haOff[m.LeagueId] = _haOff.GetValueOrDefault(m.LeagueId) * (1 - DavidsonEloConfig.HomeLeagueDecay) + DavidsonEloConfig.HomeLeagueRate * g;
            }
            var gd = yd - p.P.D; // log ν gradyanı
            _logNuGlobal += DavidsonEloConfig.NuGlobalRate * gd;
            if (_c.LeagueDraw)
                _nuOff[m.LeagueId] = _nuOff.GetValueOrDefault(m.LeagueId) * (1 - DavidsonEloConfig.NuLeagueDecay) + DavidsonEloConfig.NuLeagueRate * gd;
            Residual(h, g); Residual(a, -g);
            h.Last = m.KickoffUtc; a.Last = m.KickoffUtc;
            Applied++;
        }

        private void Residual(Team t, double r)
        {
            var decay = Math.Pow(0.5, 1 / Math.Max(0.5, _c.FormHalfLife));
            t.Form = decay * t.Form + (1 - decay) * r;
            t.Residuals.Enqueue(r);
            while (t.Residuals.Count > 10) t.Residuals.Dequeue();
        }

        private Team TeamOf(int id) => _teams.TryGetValue(id, out var t) ? t : _teams[id] = new Team();

        private void SetLeague(int id, Team t, int? league)
        {
            if (league is not int l || t.HomeLeague == l) return;
            if (t.HomeLeague is int old && _members.TryGetValue(old, out var os)) os.Remove(id);
            t.HomeLeague = l;
            if (!_members.TryGetValue(l, out var set)) _members[l] = set = new HashSet<int>();
            set.Add(id);
        }

        public double HomeAdvantageOf(int leagueId) => _haGlobal + _haOff.GetValueOrDefault(leagueId);
        public double NuOf(int leagueId) => Math.Exp(_logNuGlobal + (_c.LeagueDraw ? _nuOff.GetValueOrDefault(leagueId) : 0));
    }

    /// <summary>
    /// 1X2 ÖLÇÜMÜ VE NESTED WALK-FORWARD YARDIMCILARI (Model 6 laboratuvarı) — üretim modelini DEĞİŞTİRMEZ.
    /// </summary>
    public static class Model6Lab
    {
        public const double Eps = 1e-6;
        public static readonly string[] ClassNames = { "Ev", "Beraberlik", "Deplasman" };

        public static int Outcome(int hg, int ag) => hg > ag ? 0 : hg == ag ? 1 : 2;

        public static double LogLoss(Probs3 p, int y) => -Math.Log(Math.Max(Eps, p[y]));

        public static double Brier(Probs3 p, int y)
        {
            double s = 0;
            for (var c = 0; c < 3; c++) s += Math.Pow(p[c] - (c == y ? 1 : 0), 2);
            return s;
        }

        public sealed class Metrics
        {
            public int N { get; set; }
            public double LogLoss { get; set; }
            public double Brier { get; set; }
            /// <summary>Sınıflar birleştirilmiş (3 × N nokta) 10 kovalı ECE — üretim ölçümüyle aynı tanım.</summary>
            public double Ece { get; set; }
            public double Accuracy { get; set; }
            /// <summary>[gerçek, tahmin] karışıklık matrisi (0 ev, 1 beraberlik, 2 deplasman).</summary>
            public int[][] Confusion { get; set; } = Array.Empty<int[]>();
            public double[] Precision { get; set; } = Array.Empty<double>();
            public double[] Recall { get; set; } = Array.Empty<double>();
            public double[] MeanProbability { get; set; } = Array.Empty<double>();
            public double[] ActualRate { get; set; } = Array.Empty<double>();
        }

        public static Metrics Evaluate(IReadOnlyList<(Probs3 P, int Y)> xs)
        {
            var conf = new int[3][]; for (var i = 0; i < 3; i++) conf[i] = new int[3];
            double ll = 0, br = 0; var acc = 0;
            var pooled = new List<(double P, bool Y)>(xs.Count * 3);
            var meanP = new double[3]; var act = new double[3];
            foreach (var (p, y) in xs)
            {
                ll += LogLoss(p, y); br += Brier(p, y);
                var k = p.Argmax;
                conf[y][k]++;
                if (k == y) acc++;
                for (var c = 0; c < 3; c++) { pooled.Add((p[c], c == y)); meanP[c] += p[c]; act[c] += c == y ? 1 : 0; }
            }
            var n = Math.Max(1, xs.Count);
            return new Metrics
            {
                N = xs.Count, LogLoss = R(ll / n), Brier = R(br / n), Ece = R(OutcomeBacktest.Ece(pooled, 10)), Accuracy = R((double)acc / n),
                Confusion = conf,
                Precision = Enumerable.Range(0, 3).Select(c => { var col = conf[0][c] + conf[1][c] + conf[2][c]; return col == 0 ? double.NaN : R((double)conf[c][c] / col); }).ToArray(),
                Recall = Enumerable.Range(0, 3).Select(c => { var row = conf[c].Sum(); return row == 0 ? double.NaN : R((double)conf[c][c] / row); }).ToArray(),
                MeanProbability = meanP.Select(v => R(v / n)).ToArray(), ActualRate = act.Select(v => R(v / n)).ToArray()
            };
        }

        /// <summary>Tek sınıfın kalibrasyon eğrisi: olasılık kovası → (n, ortalama tahmin, gerçekleşme).</summary>
        public static List<(double Lo, double Hi, int N, double MeanP, double Rate)> ClassCurve(IReadOnlyList<(Probs3 P, int Y)> xs, int cls, double width = 0.1)
        {
            var list = new List<(double, double, int, double, double)>();
            for (var lo = 0.0; lo < 1 - 1e-9; lo += width)
            {
                var hi = lo + width;
                var band = xs.Where(x => x.P[cls] >= lo && (x.P[cls] < hi || (hi >= 1 - 1e-9 && x.P[cls] <= 1))).ToList();
                if (band.Count == 0) continue;
                list.Add((R(lo), R(hi), band.Count, R(band.Average(b => b.P[cls])), R(band.Average(b => b.Y == cls ? 1.0 : 0.0))));
            }
            return list;
        }

        // ═══════════════════════ KALİBRASYON (yalnız iç validation'da uydurulur) ═══════════════════════

        /// <summary>Sıcaklık ölçekleme: p_c ∝ p_c^{1/T}.</summary>
        public static Probs3 Temperature(Probs3 p, double t)
        {
            var e = 1 / Math.Max(0.05, t);
            return Probs3.Normalized(Math.Pow(p.H, e), Math.Pow(p.D, e), Math.Pow(p.A, e));
        }

        /// <summary>Vektör ölçekleme: log p_c / T + b_c (b_ev = 0 referans).</summary>
        public static Probs3 Vector(Probs3 p, double t, double bDraw, double bAway)
        {
            var e = 1 / Math.Max(0.05, t);
            return Probs3.Normalized(Math.Pow(p.H, e), Math.Pow(p.D, e) * Math.Exp(bDraw), Math.Pow(p.A, e) * Math.Exp(bAway));
        }

        public static double FitTemperature(IReadOnlyList<(Probs3 P, int Y)> xs)
        {
            double best = 1, bestL = double.MaxValue;
            for (var t = 0.80; t <= 1.2001; t += 0.01)
            {
                var l = xs.Average(x => LogLoss(Temperature(x.P, t), x.Y));
                if (l < bestL - 1e-12) { bestL = l; best = Math.Round(t, 2); }
            }
            return best;
        }

        public static (double T, double BDraw, double BAway) FitVector(IReadOnlyList<(Probs3 P, int Y)> xs)
        {
            var t = FitTemperature(xs); double bd = 0, ba = 0;
            // Koordinat araması (3 tur), düzenli kalibrasyon: sapmalar ±0,3 ile sınırlı.
            for (var round = 0; round < 3; round++)
            {
                bd = Argmin(v => xs.Average(x => LogLoss(Vector(x.P, t, v, ba), x.Y)), -0.3, 0.3, 0.01);
                ba = Argmin(v => xs.Average(x => LogLoss(Vector(x.P, t, bd, v), x.Y)), -0.3, 0.3, 0.01);
                t = Argmin(v => xs.Average(x => LogLoss(Vector(x.P, v, bd, ba), x.Y)), 0.8, 1.2, 0.01);
            }
            return (t, bd, ba);
        }

        private static double Argmin(Func<double, double> f, double lo, double hi, double step)
        {
            double best = 0, bestL = double.MaxValue;
            for (var v = lo; v <= hi + 1e-9; v += step)
            {
                var l = f(v);
                // Eşitlikte nötre (0 ya da 1) en yakın değer: gereksiz sapma seçilmez.
                if (l < bestL - 1e-12) { bestL = l; best = Math.Round(v, 4); }
            }
            return best;
        }

        /// <summary>Doğrusal havuz: (1 − Σw)·taban + Σ w_i·diğer_i.</summary>
        public static Probs3 Pool(Probs3 baseP, IReadOnlyList<(Probs3 P, double W)> others)
        {
            var w0 = 1 - others.Sum(o => o.W);
            double h = w0 * baseP.H, d = w0 * baseP.D, a = w0 * baseP.A;
            foreach (var (p, w) in others) { h += w * p.H; d += w * p.D; a += w * p.A; }
            return Probs3.Normalized(h, d, a);
        }

        /// <summary>
        /// DIŞ FOLD SINIRLARI — manifest maçları tarih sırasına göre eşit sayılı <paramref name="folds"/> parçaya bölünür; sınır
        /// her zaman bir takvim gününün başıdır (aynı gün iki fold'a bölünmez). Yalnız başlama saatleri kullanılır (sonuç değil).
        /// </summary>
        public static List<(DateTime From, DateTime To)> OuterFolds(IReadOnlyList<DateTime> kickoffs, DateTime start, DateTime end, int folds)
        {
            var sorted = kickoffs.OrderBy(x => x).ToList();
            var bounds = new List<DateTime> { start };
            for (var k = 1; k < folds; k++)
            {
                var d = sorted[(int)((long)sorted.Count * k / folds)].Date;
                if (d <= bounds[^1]) d = bounds[^1].AddDays(1);
                bounds.Add(DateTime.SpecifyKind(d, DateTimeKind.Utc));
            }
            bounds.Add(end);
            return Enumerable.Range(0, folds).Select(k => (bounds[k], bounds[k + 1])).ToList();
        }

        private static double R(double v) => Math.Round(v, 5);
    }
}
