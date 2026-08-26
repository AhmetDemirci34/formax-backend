using System;
using System.Collections.Generic;
using Formax.Application.AI.Signals;

namespace Formax.Application.AI.Decision
{
    /// <summary>
    /// Motor iç-boru hattı ara tipleri (public API DEĞİL). Modüller arası akış:
    /// AiSignal → UnderstoodSignal → WeightedSignal → SignalField / PoissonGoalModel.
    /// Hepsi immutable (record) ve deterministik.
    /// </summary>
    internal readonly record struct UnderstoodSignal(
        AiSignal Source,
        string Name,
        string Category,
        double Impact,          // -1..+1 yönlü (0 = yönsüz/severity)
        double Magnitude,       // 0..1 büyüklük
        bool IsDirectional,     // true = 1X2 yönünü etkiler, false = tempo/severity
        SignalConflictStatus Conflict,
        bool HasData);

    /// <summary>Dinamik ağırlık atanmış sinyal. Weight = kalite × etki (sabit ağırlık YOK).</summary>
    internal readonly record struct WeightedSignal(
        UnderstoodSignal Signal,
        double Weight,          // 0..1 ham dinamik ağırlık
        bool Suppressed);       // çelişki/bayatlık nedeniyle baskılandı mı

    /// <summary>
    /// Ağırlıklı sinyallerin agregat "alanı" — motorun beklenen-gol ve yön kararının girdisi.
    /// </summary>
    internal readonly record struct SignalField(
        double NetHomeEdge,     // -1..+1 (+ ev lehine) ağırlıklı yönlü toplam
        double AttackPressure,  // 0..1 toplam hücum/tempo baskısı
        double Instability,     // 0..1 çelişki/breaking kaynaklı belirsizlik
        double AvgConfidence,   // 0..1
        int ActiveCount);

    /// <summary>
    /// Deterministik Poisson gol modeli. İki bağımsız Poisson (ev/dep beklenen gol) üzerinden
    /// tam skor matrisini kurar; TÜM market olasılıkları bu tek matristen türetilir
    /// (1X2, alt/üst, KG, ilk gol, toplam-aralık, skor-aralık, ilk yarı). Standart, açıklanabilir
    /// istatistik — mock/fake değil.
    /// </summary>
    internal sealed class PoissonGoalModel
    {
        private const int MaxGoals = 8; // 0..8 gol; kuyruk ihmal edilebilir
        private readonly double[,] _joint; // P(homeGoals=i, awayGoals=j)

        public double ExpHome { get; }
        public double ExpAway { get; }

        public PoissonGoalModel(double expHome, double expAway)
        {
            ExpHome = Math.Max(0.05, expHome);
            ExpAway = Math.Max(0.05, expAway);

            var hp = Pmf(ExpHome);
            var ap = Pmf(ExpAway);
            _joint = new double[MaxGoals + 1, MaxGoals + 1];
            for (int i = 0; i <= MaxGoals; i++)
                for (int j = 0; j <= MaxGoals; j++)
                    _joint[i, j] = hp[i] * ap[j];
        }

        // ── 1X2 ──────────────────────────────────────────────────────────────
        public double PHomeWin()
        {
            double p = 0;
            for (int i = 0; i <= MaxGoals; i++)
                for (int j = 0; j < i; j++) p += _joint[i, j];
            return p;
        }

        public double PDraw()
        {
            double p = 0;
            for (int i = 0; i <= MaxGoals; i++) p += _joint[i, i];
            return p;
        }

        public double PAwayWin() => Math.Max(0, 1.0 - PHomeWin() - PDraw());

        // ── Toplam gol alt/üst ────────────────────────────────────────────────
        public double POver(double line)
        {
            double p = 0;
            for (int i = 0; i <= MaxGoals; i++)
                for (int j = 0; j <= MaxGoals; j++)
                    if (i + j > line) p += _joint[i, j];
            return p;
        }

        public double PUnder(double line) => Math.Max(0, 1.0 - POver(line));

        // ── Karşılıklı gol ────────────────────────────────────────────────────
        public double PBtts()
        {
            double p = 0;
            for (int i = 1; i <= MaxGoals; i++)
                for (int j = 1; j <= MaxGoals; j++)
                    p += _joint[i, j];
            return p;
        }

        // ── Toplam gol aralığı (a..b gol dahil) ───────────────────────────────
        public double PTotalRange(int lo, int hi)
        {
            double p = 0;
            for (int i = 0; i <= MaxGoals; i++)
                for (int j = 0; j <= MaxGoals; j++)
                {
                    var t = i + j;
                    if (t >= lo && t <= hi) p += _joint[i, j];
                }
            return p;
        }

        /// <summary>En olası toplam-gol bandını (0-1 | 2-3 | 4+) döndürür.</summary>
        public (string label, double prob) MostLikelyTotalBand()
        {
            var b01 = PTotalRange(0, 1);
            var b23 = PTotalRange(2, 3);
            var b4 = PTotalRange(4, MaxGoals * 2);
            if (b01 >= b23 && b01 >= b4) return ("0-1 gol", b01);
            if (b23 >= b4) return ("2-3 gol", b23);
            return ("4+ gol", b4);
        }

        /// <summary>En olası tam skoru döndürür (skor aralığı temsili).</summary>
        public (int h, int a, double prob) MostLikelyScore()
        {
            int bh = 0, ba = 0; double best = -1;
            for (int i = 0; i <= MaxGoals; i++)
                for (int j = 0; j <= MaxGoals; j++)
                    if (_joint[i, j] > best) { best = _joint[i, j]; bh = i; ba = j; }
            return (bh, ba, best);
        }

        // ── İlk gol (gol atma oranı payı; gol olma olasılığıyla ölçekli) ──────
        public double PFirstGoalHome()
        {
            var share = ExpHome / (ExpHome + ExpAway);
            var anyGoal = 1.0 - _joint[0, 0];
            return share * anyGoal;
        }

        public double PFirstGoalAway()
        {
            var share = ExpAway / (ExpHome + ExpAway);
            var anyGoal = 1.0 - _joint[0, 0];
            return share * anyGoal;
        }

        /// <summary>Poisson PMF vektörü (0..MaxGoals).</summary>
        private static double[] Pmf(double lambda)
        {
            var p = new double[MaxGoals + 1];
            var term = Math.Exp(-lambda); // P(0)
            p[0] = term;
            for (int k = 1; k <= MaxGoals; k++)
            {
                term *= lambda / k;
                p[k] = term;
            }
            return p;
        }
    }
}
