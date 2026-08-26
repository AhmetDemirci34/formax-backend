using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Telemetry;

namespace Formax.Infrastructure.Http
{
    /// <summary>
    /// Timeline Operations — her giden api-football HTTP isteğini ApiFootballMetrics'e kaydeder.
    /// GERÇEK ölçüm: total/failed + endpoint ailesi. Davranışı DEĞİŞTİRMEZ (yalnız sayar, isteği
    /// olduğu gibi geçirir). ApiFootball HttpClient pipeline'ına eklenir.
    /// </summary>
    public sealed class ApiFootballMeteringHandler : DelegatingHandler
    {
        private readonly ApiFootballMetrics _metrics;

        public ApiFootballMeteringHandler(ApiFootballMetrics metrics)
        {
            _metrics = metrics;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var family = ClassifyEndpoint(request.RequestUri);
            try
            {
                var response = await base.SendAsync(request, cancellationToken);
                _metrics.RecordRequest(family, response.IsSuccessStatusCode);
                return response;
            }
            catch
            {
                _metrics.RecordRequest(family, success: false);
                throw;
            }
        }

        // Sınıflandırma ApiFootballEndpointFamily'e taşındı (mantık BİREBİR aynı) — böylece
        // job-attribution handler'ı da aynı aile adlarını üretir. Davranış değişmedi.
        private static string ClassifyEndpoint(Uri? uri) => ApiFootballEndpointFamily.Classify(uri);
    }
}
