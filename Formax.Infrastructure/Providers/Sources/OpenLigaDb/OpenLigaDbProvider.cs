using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Providers.Abstractions;
using Formax.Infrastructure.Providers.Configuration;
using Formax.Infrastructure.Providers.Health;

namespace Formax.Infrastructure.Providers.Sources.OpenLigaDb;

/// <summary>
/// OpenLigaDB (api.openligadb.de) — FORMAX Core Strategy'de onaylanan ilk ücretsiz/açık provider.
/// Anahtar gerektirmez. Capability'ler: Fixture, Standings, Team.
///
/// Bu sınıf HİÇBİR ayar tutmaz (BaseUrl / Timeout / Retry / Priority / RefreshInterval / lig / sezon).
/// Tüm ayarları merkezi <see cref="IProviderConfigurationService"/>'ten okur; ayarların KAYNAĞINI bilmez.
/// Manifest yalnızca KİMLİK bildirir (ad + capability + kategori). Sağlık merkezi servise raporlanır.
///
/// FAZ 13 kapsamı: yalnızca HAM veri getirir (normalize/merge/identity/coverage YOK).
/// </summary>
public sealed class OpenLigaDbProvider : IFixtureProvider, IStandingsProvider, ITeamProvider
{
    private readonly HttpClient _http;
    private readonly IProviderConfigurationService _configuration;
    private readonly IProviderHealthService _health;

    public OpenLigaDbProvider(
        HttpClient http,
        IProviderConfigurationService configuration,
        IProviderHealthService health)
    {
        _http = http;
        _configuration = configuration;
        _health = health;
    }

    public ProviderManifest Manifest { get; } = ProviderManifest.Create(
        name: "openligadb",
        capabilities: new[]
        {
            ProviderCapability.Fixture,
            ProviderCapability.Standings,
            ProviderCapability.Team
        },
        category: ProviderCategory.Free);

    public Task<ProviderResult> FetchFixturesAsync(ProviderRequest request, CancellationToken cancellationToken = default)
        => GetRawAsync(ProviderCapability.Fixture, "getmatchdata", cancellationToken);

    public Task<ProviderResult> FetchStandingsAsync(ProviderRequest request, CancellationToken cancellationToken = default)
        => GetRawAsync(ProviderCapability.Standings, "getbltable", cancellationToken);

    public Task<ProviderResult> FetchTeamsAsync(ProviderRequest request, CancellationToken cancellationToken = default)
        => GetRawAsync(ProviderCapability.Team, "getavailableteams", cancellationToken);

    private async Task<ProviderResult> GetRawAsync(ProviderCapability capability, string endpoint, CancellationToken cancellationToken)
    {
        // Ayarların TAMAMI merkezi yapılandırmadan gelir; provider hiçbir varsayılan taşımaz.
        var config = _configuration.Get(Manifest.Name, capability);
        if (config is null || string.IsNullOrWhiteSpace(config.BaseUrl))
            return ProviderResult.Fail(Manifest.Name, $"{capability} için merkezi yapılandırma (BaseUrl) bulunamadı.");

        var league = Param(config, "league");
        var season = Param(config, "season");
        if (string.IsNullOrWhiteSpace(league) || string.IsNullOrWhiteSpace(season))
            return ProviderResult.Fail(Manifest.Name, $"{capability} için merkezi yapılandırmada 'league'/'season' eksik.");

        var url = $"{config.BaseUrl.TrimEnd('/')}/{endpoint}/{league}/{season}";

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (config.Timeout > TimeSpan.Zero)
                timeoutCts.CancelAfter(config.Timeout);

            using var message = new HttpRequestMessage(HttpMethod.Get, url);
            foreach (var header in config.Headers)
                message.Headers.TryAddWithoutValidation(header.Key, header.Value);
            message.Headers.TryAddWithoutValidation("Accept", "application/json");

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

    private static string? Param(ProviderConfiguration config, string key) =>
        config.QueryParameters.TryGetValue(key, out var value) ? value : null;
}
