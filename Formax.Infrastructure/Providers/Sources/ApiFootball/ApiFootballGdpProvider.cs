using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.DTOs.Fixtures;
using Formax.Application.Interfaces;
using Formax.Infrastructure.Http;
using Formax.Infrastructure.Providers.Abstractions;
using Formax.Infrastructure.Providers.Health;
using Microsoft.Extensions.Configuration;

namespace Formax.Infrastructure.Providers.Sources.ApiFootball;

/// <summary>
/// api-football → GDP köprüsü (ADAPTER).
///
/// NEDEN ADAPTER: api-football zaten <see cref="ISportsDataProvider"/> üzerinden çağrılıyor ve o yol
/// cache, metering (ApiFootballMetrics), rate-limit koruması ve timezone mantığını taşıyor. GDP için
/// İKİNCİ bir HTTP istemcisi kurmak bu korumaların hepsini baypas ederdi. Bu sınıf yeni HTTP çağrısı
/// AÇMAZ: mevcut sağlayıcıyı sarmalar, dolayısıyla kota davranışı birebir korunur.
///
/// Capability: yalnız Fixture. Kategori <see cref="ProviderCategory.Paid"/> — FORMAX çekirdek kuralı
/// gereği sistem ücretsiz sağlayıcılarla tam çalışabilmelidir; bu sağlayıcı her zaman OPSİYONELDİR.
///
/// ProviderMatchId = api-football fixture id. Bu değer Identity aşamasında
/// <c>GdpProviderMatchReference</c>'a yazılır ve Persist aşamasında canonical <c>Match.ExternalMatchId</c>
/// ile birebir eşleşmeyi sağlar → aynı maç ikinci kez oluşturulmaz.
/// </summary>
public sealed class ApiFootballGdpProvider : IFixtureProvider
{
    /// <summary>GDP tarafındaki sağlayıcı adı. Mapper seçimi ve provider referansları bu adı kullanır.</summary>
    public const string Name = "api-football";

    private readonly ISportsDataProvider _sports;
    private readonly IProviderHealthService _health;
    private readonly IConfiguration _configuration;

    public ApiFootballGdpProvider(
        ISportsDataProvider sports,
        IProviderHealthService health,
        IConfiguration configuration)
    {
        _sports = sports;
        _health = health;
        _configuration = configuration;
    }

    public ProviderManifest Manifest { get; } = ProviderManifest.Create(
        name: Name,
        capabilities: new[] { ProviderCapability.Fixture },
        category: ProviderCategory.Paid);

    public async Task<ProviderResult> FetchFixturesAsync(
        ProviderRequest request, CancellationToken cancellationToken = default)
    {
        // Tarih aralığı istekten gelir. Verilmediyse tek gün (bugün) — global tarama YAPILMAZ,
        // aksi halde tek pipeline çalıştırması kotayı beklenmedik şekilde tüketebilirdi.
        //
        // TAKVİM KONVANSİYONU: ApiFootballSportsDataProvider her fixtures çağrısına
        // "&timezone=Europe/Istanbul" ekler; bu yüzden "date" parametresi UTC değil YEREL takvim
        // günüdür. FixtureSyncJob da gün penceresini böyle hesaplar (ConvertTimeFromUtc + .Date).
        // UTC gününü göndermek, gece yarısına yakın maçlarda yanlış günü sorgular
        // (ör. 21:30 UTC = ertesi gün 00:30 Europe/Istanbul).
        var tz = ApiFootballTimeZone.TryResolve(_configuration[ApiFootballTimeZone.ConfigKey])
                 ?? TimeZoneInfo.Utc;

        var fromUtc = (request?.FromUtc ?? DateTimeOffset.UtcNow).UtcDateTime;
        var toUtc = (request?.ToUtc ?? request?.FromUtc ?? DateTimeOffset.UtcNow).UtcDateTime;
        if (toUtc < fromUtc) toUtc = fromUtc;

        var from = TimeZoneInfo.ConvertTimeFromUtc(fromUtc, tz).Date;
        var to = TimeZoneInfo.ConvertTimeFromUtc(toUtc, tz).Date;
        if (to < from) to = from;

        var stopwatch = Stopwatch.StartNew();
        try
        {
            // MEVCUT yol: cache + metering + rate-limit burada devrede. Yeni endpoint eklenmedi.
            var fixtures = await _sports.GetFixturesAsync(from, to, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            _health.RecordSuccess(Manifest.Name, ProviderCapability.Fixture, stopwatch.Elapsed);

            var payload = fixtures ?? new List<SportsFixtureResult>();

            // MATCH-SCOPE DARALTMA: /fixtures?date= günün TÜM maçlarını döndürür. Belirli bir maç
            // için çalışılıyorsa (request.ExternalMatchId dolu) yalnız o fikstür bırakılır; böylece
            // Normalize/Identity/Merge/Persist aşamalarına tek maç girer.
            // Eşleşme yoksa BOŞ sonuç döner — uydurma fikstür üretilmez.
            // Alan null ise (global keşif) liste olduğu gibi geçer: mevcut davranış korunur.
            var scope = request?.ExternalMatchId;
            if (!string.IsNullOrWhiteSpace(scope))
            {
                payload = payload
                    .Where(f => f is not null &&
                                string.Equals(f.ExternalMatchId?.Trim(), scope.Trim(), StringComparison.Ordinal))
                    .ToList();
            }

            // Payload tipli taşınır (JSON'a geri serileştirme yok); mapper bu tipi bekler.
            return ProviderResult.Ok(Manifest.Name, payload);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _health.RecordFailure(Manifest.Name, ProviderCapability.Fixture, stopwatch.Elapsed, ex.Message);
            return ProviderResult.Fail(Manifest.Name, ex.Message);
        }
    }
}
