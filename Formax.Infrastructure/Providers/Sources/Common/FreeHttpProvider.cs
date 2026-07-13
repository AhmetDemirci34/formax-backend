using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Providers.Abstractions;
using Formax.Infrastructure.Providers.Configuration;
using Formax.Infrastructure.Providers.Health;

namespace Formax.Infrastructure.Providers.Sources.Common;

/// <summary>
/// Ücretsiz/açık HTTP provider'ları için ORTAK temel (kod tekrarını önler).
/// Ayarları merkezi <see cref="IProviderConfigurationService"/>'ten okur, sağlığı merkezi
/// <see cref="IProviderHealthService"/>'e raporlar, ham gövdeyi <see cref="ProviderResult"/> olarak döndürür.
/// Somut provider yalnızca KİMLİĞİNİ (Manifest) ve capability→URL kurulumunu içerir; ayar tutmaz.
/// </summary>
public abstract class FreeHttpProvider
{
    private readonly HttpClient _http;
    private readonly IProviderConfigurationService _configuration;
    private readonly IProviderHealthService _health;

    protected FreeHttpProvider(HttpClient http, IProviderConfigurationService configuration, IProviderHealthService health)
    {
        _http = http;
        _configuration = configuration;
        _health = health;
    }

    public abstract ProviderManifest Manifest { get; }

    /// <summary>Merkezi yapılandırmadan URL kurup GET yapar; sağlığı raporlar; ham gövdeyi döndürür.</summary>
    protected async Task<ProviderResult> GetAsync(
        ProviderCapability capability,
        Func<ProviderConfiguration, string?> urlBuilder,
        CancellationToken cancellationToken)
    {
        var config = _configuration.Get(Manifest.Name, capability);
        if (config is null || string.IsNullOrWhiteSpace(config.BaseUrl))
            return ProviderResult.Fail(Manifest.Name, $"{capability} için merkezi yapılandırma (BaseUrl) yok.");

        string? url;
        try { url = urlBuilder(config); }
        catch { url = null; }

        if (string.IsNullOrWhiteSpace(url))
            return ProviderResult.Fail(Manifest.Name, $"{capability} için URL üretilemedi (parametre eksik).");

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (config.Timeout > TimeSpan.Zero)
                timeoutCts.CancelAfter(config.Timeout);

            using var message = new HttpRequestMessage(HttpMethod.Get, url);
            foreach (var header in config.Headers)
                message.Headers.TryAddWithoutValidation(header.Key, header.Value);
            message.Headers.TryAddWithoutValidation("User-Agent", "FORMAX-GDP/1.0 (+https://formax)");
            message.Headers.TryAddWithoutValidation("Accept", "application/json, text/csv, application/xml, */*");

            using var response = await _http.SendAsync(message, timeoutCts.Token).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);
            stopwatch.Stop();

            if (!response.IsSuccessStatusCode)
            {
                _health.RecordFailure(Manifest.Name, capability, stopwatch.Elapsed, $"HTTP {(int)response.StatusCode}");
                return ProviderResult.Fail(Manifest.Name, $"HTTP {(int)response.StatusCode} — {url}");
            }

            _health.RecordSuccess(Manifest.Name, capability, stopwatch.Elapsed);
            return ProviderResult.Ok(Manifest.Name, body);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _health.RecordFailure(Manifest.Name, capability, stopwatch.Elapsed, ex.Message);
            return ProviderResult.Fail(Manifest.Name, ex.Message);
        }
    }

    protected static string? Param(ProviderConfiguration config, string key) =>
        config.QueryParameters.TryGetValue(key, out var value) ? value : null;
}
