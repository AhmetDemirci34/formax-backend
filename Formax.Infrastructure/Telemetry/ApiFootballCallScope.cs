using System;
using System.Threading;

namespace Formax.Infrastructure.Telemetry
{
    /// <summary>
    /// Job attribution — hangi job'ın api-football isteği ürettiğini taşıyan ORTAM bağlamı.
    ///
    /// NEDEN: <see cref="Http.ApiFootballMeteringHandler"/> yalnız <c>HttpRequestMessage</c> görür;
    /// çağıran job'ı bilemez. Bu yüzden endpoint ailesi (<c>fixtures</c>, <c>injuries</c>) birden
    /// fazla job tarafından paylaşıldığında kota job bazında ayrıştırılamıyordu.
    ///
    /// NASIL: Job, turunun başında <see cref="Begin"/> ile adını ilan eder; değer
    /// <see cref="AsyncLocal{T}"/> ile await zinciri boyunca akar ve HTTP gönderim anında
    /// <see cref="Http.ApiFootballJobAttributionHandler"/> tarafından okunur.
    ///
    /// SALT-ÖLÇÜM: hiçbir davranışı etkilemez — endpoint, cache, retry, job mantığı, istek sayısı
    /// AYNEN kalır. Kapsam ilan edilmemişse istek <c>"unattributed"</c> olarak sayılır.
    /// </summary>
    public static class ApiFootballCallScope
    {
        /// <summary>Kapsam ilan edilmemiş isteklerin etiketi.</summary>
        public const string Unattributed = "unattributed";

        private static readonly AsyncLocal<string?> _current = new();

        /// <summary>Şu anki job adı; ilan edilmemişse <see cref="Unattributed"/>.</summary>
        public static string Current => _current.Value ?? Unattributed;

        /// <summary>
        /// Bu noktadan itibaren (await zinciri boyunca) üretilen api-football isteklerini
        /// <paramref name="jobName"/>'e etiketler. Dispose edildiğinde ÖNCEKİ değer geri gelir
        /// (iç içe kapsam güvenli).
        /// </summary>
        public static IDisposable Begin(string jobName)
        {
            var previous = _current.Value;
            _current.Value = string.IsNullOrWhiteSpace(jobName) ? Unattributed : jobName;
            return new Restore(previous);
        }

        private sealed class Restore : IDisposable
        {
            private readonly string? _previous;
            private bool _disposed;

            public Restore(string? previous) => _previous = previous;

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _current.Value = _previous;
            }
        }
    }
}
