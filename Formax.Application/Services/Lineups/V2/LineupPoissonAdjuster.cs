using System;
using System.Collections.Generic;
using System.Linq;

namespace Formax.Application.Services.Lineups.V2
{
    /// <summary>Tek eğitim satırı: bir takımın bir maçtaki attığı gol, taban beklentisi ve özellikleri.</summary>
    public sealed record PoissonRow(double[] Features, double BaseLambda, int Goals);

    /// <summary>
    /// TABAN ÜSTÜNE DÜZENLİLEŞTİRİLMİŞ POISSON DÜZELTMESİ.
    ///
    ///     log μ = log(λ_taban)  +  a·x
    ///
    /// λ_taban OFFSET'tir: Outcome Model 4.0 DEĞİŞMEZ, üstüne yalnız katsayıları öğrenilen
    /// sınırlı bir delta biner. Katsayılar L2 (ridge) cezasıyla Newton-Raphson ile çözülür;
    /// ceza gücü YALNIZ doğrulama fold'larında seçilir, holdout'a bakılarak değil.
    ///
    /// Elle yazılmış etki yüzdesi YOKTUR: bütün katsayılar veriden gelir.
    /// </summary>
    public sealed class LineupPoissonAdjuster
    {
        /// <summary>λ üzerindeki toplam log deltanın mutlak tavanı — mevcut üretim tavanıyla AYNI, büyütülmedi.</summary>
        public const double MaxLogDelta = 0.10;

        private readonly double[] _coefficients;
        private readonly double[] _mean;
        private readonly double[] _scale;

        public IReadOnlyList<double> Coefficients => _coefficients;
        public double Ridge { get; }
        public int TrainingRows { get; }
        public bool Converged { get; }

        private LineupPoissonAdjuster(double[] coefficients, double[] mean, double[] scale, double ridge, int rows, bool converged)
        {
            _coefficients = coefficients; _mean = mean; _scale = scale;
            Ridge = ridge; TrainingRows = rows; Converged = converged;
        }

        /// <summary>Hiç katsayı öğrenilmemiş model — her delta 0 (taban birebir korunur).</summary>
        public static LineupPoissonAdjuster Zero(int dimension)
            => new(new double[dimension], new double[dimension], Enumerable.Repeat(1.0, dimension).ToArray(), 0, 0, true);

        /// <summary>
        /// Eğitim. Özellikler EĞİTİM fold'unun ortalama/standart sapmasıyla ölçeklenir; aynı ölçek
        /// tahmin anında uygulanır (doğrulama/holdout istatistiği eğitime SIZMAZ).
        /// </summary>
        public static LineupPoissonAdjuster Fit(IReadOnlyList<PoissonRow> rows, double ridge, int maxIterations = 40)
        {
            if (rows.Count == 0) return Zero(0);
            var d = rows[0].Features.Length;
            if (d == 0 || rows.Count < d * 10) return Zero(d); // örneklem boyuta göre yetersizse katsayı ÜRETİLMEZ

            var mean = new double[d];
            var scale = new double[d];
            for (var j = 0; j < d; j++)
            {
                mean[j] = rows.Average(r => r.Features[j]);
                var variance = rows.Average(r => Math.Pow(r.Features[j] - mean[j], 2));
                scale[j] = variance <= 1e-12 ? 1.0 : Math.Sqrt(variance);
            }

            var x = rows.Select(r => Standardize(r.Features, mean, scale)).ToArray();
            var offset = rows.Select(r => Math.Log(Math.Max(1e-6, r.BaseLambda))).ToArray();
            var y = rows.Select(r => (double)r.Goals).ToArray();

            var a = new double[d];
            var converged = false;
            for (var iter = 0; iter < maxIterations; iter++)
            {
                var gradient = new double[d];
                var hessian = new double[d, d];
                for (var i = 0; i < x.Length; i++)
                {
                    var eta = offset[i] + Dot(a, x[i]);
                    var mu = Math.Exp(Math.Clamp(eta, -8, 4));
                    var residual = y[i] - mu;
                    for (var j = 0; j < d; j++)
                    {
                        gradient[j] += residual * x[i][j];
                        for (var k = 0; k < d; k++) hessian[j, k] += mu * x[i][j] * x[i][k];
                    }
                }
                for (var j = 0; j < d; j++)
                {
                    gradient[j] -= ridge * a[j];
                    hessian[j, j] += ridge;
                }

                var step = Solve(hessian, gradient);
                if (step == null) break;
                var delta = 0.0;
                for (var j = 0; j < d; j++) { a[j] += step[j]; delta += Math.Abs(step[j]); }
                if (delta < 1e-9) { converged = true; break; }
            }

            return new LineupPoissonAdjuster(a, mean, scale, ridge, rows.Count, converged);
        }

        /// <summary>λ üzerindeki log delta — tavanla sınırlı. Katsayı yoksa 0.</summary>
        public double LogDelta(double[] features)
        {
            if (_coefficients.Length == 0 || features.Length != _coefficients.Length) return 0;
            var z = Dot(_coefficients, Standardize(features, _mean, _scale));
            if (double.IsNaN(z) || double.IsInfinity(z)) return 0;
            return Math.Clamp(z, -MaxLogDelta, MaxLogDelta);
        }

        /// <summary>Tavanın gerçekten devreye girip girmediği (tavan kullanım oranı raporu için).</summary>
        public bool WouldClamp(double[] features)
        {
            if (_coefficients.Length == 0 || features.Length != _coefficients.Length) return false;
            var z = Dot(_coefficients, Standardize(features, _mean, _scale));
            return Math.Abs(z) > MaxLogDelta;
        }

        public double RawDelta(double[] features)
        {
            if (_coefficients.Length == 0 || features.Length != _coefficients.Length) return 0;
            return Dot(_coefficients, Standardize(features, _mean, _scale));
        }

        private static double[] Standardize(double[] raw, double[] mean, double[] scale)
        {
            var z = new double[raw.Length];
            for (var j = 0; j < raw.Length; j++) z[j] = (raw[j] - mean[j]) / scale[j];
            return z;
        }

        private static double Dot(double[] a, double[] b)
        {
            double s = 0;
            for (var i = 0; i < a.Length; i++) s += a[i] * b[i];
            return s;
        }

        /// <summary>Gauss eliminasyonu (kısmi pivot). Tekil matriste null döner ve katsayı güncellenmez.</summary>
        private static double[]? Solve(double[,] matrix, double[] rhs)
        {
            var n = rhs.Length;
            var m = new double[n, n + 1];
            for (var i = 0; i < n; i++)
            {
                for (var j = 0; j < n; j++) m[i, j] = matrix[i, j];
                m[i, n] = rhs[i];
            }
            for (var col = 0; col < n; col++)
            {
                var pivot = col;
                for (var r = col + 1; r < n; r++) if (Math.Abs(m[r, col]) > Math.Abs(m[pivot, col])) pivot = r;
                if (Math.Abs(m[pivot, col]) < 1e-12) return null;
                if (pivot != col) for (var j = 0; j <= n; j++) (m[col, j], m[pivot, j]) = (m[pivot, j], m[col, j]);
                for (var r = 0; r < n; r++)
                {
                    if (r == col) continue;
                    var f = m[r, col] / m[col, col];
                    if (f == 0) continue;
                    for (var j = col; j <= n; j++) m[r, j] -= f * m[col, j];
                }
            }
            var result = new double[n];
            for (var i = 0; i < n; i++) result[i] = m[i, n] / m[i, i];
            return result;
        }
    }
}
