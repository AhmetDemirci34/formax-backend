using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Telemetry;

namespace Formax.Infrastructure.Http
{
    /// <summary>
    /// Job-bazlı kota ölçümü — her GERÇEK giden api-football HTTP isteğini
    /// (job × endpoint ailesi) olarak sayar.
    ///
    /// KONUM: pipeline'ın EN İÇİNE kaydedilir (ApiFootballResilienceHandler'dan SONRA).
    /// Bunun iki sonucu vardır:
    ///   • Retry'ler ayrı ayrı sayılır — resilience her denemede iç handler'ı çağırır, yani
    ///     4 denemeli bir çağrı burada 4 gerçek HTTP isteği olarak görünür.
    ///   • Mevcut <see cref="ApiFootballMeteringHandler"/> EN DIŞTA kalır; onun ürettiği
    ///     <c>RequestsByEndpoint</c> sayacının anlamı (mantıksal çağrı başına 1) DEĞİŞMEZ.
    ///
    /// CACHE: sağlayıcı cache-hit'te HTTP'ye hiç gitmez → bu handler çalışmaz → cache hit
    /// gerçek istek olarak SAYILMAZ (istenen davranış).
    ///
    /// SALT-ÖLÇÜM: isteği olduğu gibi geçirir; endpoint/cache/retry/job mantığını etkilemez.
    /// </summary>
    public sealed class ApiFootballJobAttributionHandler : DelegatingHandler
    {
        private readonly ApiFootballMetrics _metrics;

        public ApiFootballJobAttributionHandler(ApiFootballMetrics metrics)
        {
            _metrics = metrics;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var job = ApiFootballCallScope.Current;
            var family = ApiFootballEndpointFamily.Classify(request.RequestUri);

            // Deneme YAPILMADAN önce sayılır: istisna ile biten denemeler de gerçek HTTP
            // trafiğidir ve kotayı tüketmiş sayılmalıdır.
            _metrics.RecordJobRequest(job, family);

            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
    }
}
