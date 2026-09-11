using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Application.Services.PostMatch;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.PostMatch
{
    /// <summary>
    /// SAĞLAYICI ZİNCİRİ — resmî video keşfinin TEK giriş noktası.
    ///
    /// NEDEN ZİNCİR, NEDEN TEK SAĞLAYICI DEĞİL (ölçüldü 07.09.2026): keşfin tamamı tek
    /// bir YouTube RSS akışına bağlıydı ve o akış kanalın YALNIZ son ~15 videosunu
    /// verir. Yayın yoğun bir hafta sonunda resmî özet, daha bakılmadan akıştan düşüyor
    /// ve maç kalıcı olarak "video yok" durumunda kalıyordu. Tek bir kaynağın teknik
    /// sınırı, ürünün sınırı hâline gelmişti.
    ///
    /// SIRA HAK SAHİPLİĞİDİR (<see cref="OfficialVideoSourceTiers"/>): federasyon → lig →
    /// yayıncı → ev sahibi kulüp → deplasman kulübü → lisanslı spor kaynağı → yardımcı
    /// keşif (YouTube RSS). Aynı maçın özetini birden çok resmî kaynak yayımladığında
    /// ana kayıt hak sahibininkidir.
    ///
    /// YAPILANDIRILMAMIŞ SAĞLAYICI HATA DEĞİLDİR: <see cref="VideoProviderStatuses.NotConfigured"/>
    /// olan sağlayıcı hiç çağrılmaz, durumu kayda geçer ve zincir yapılandırılmış
    /// olanlarla DEVAM EDER. Eksik erişimi "yaratıcı" yollarla telafi etmek — anahtar
    /// üretmek, gizli uç aramak, sayfa kazımak — bu sınıfın yapmadığı ve yapmayacağı
    /// şeydir.
    ///
    /// BİR SAĞLAYICININ HATASI ZİNCİRİ DURDURMAZ: kaynak tek tek çöker, ürün çökmez.
    /// </summary>
    public sealed class CompositeOfficialMatchVideoProvider : IOfficialMatchVideoProvider
    {
        private readonly IReadOnlyList<IOfficialMatchVideoProvider> _providers;
        private readonly ILogger<CompositeOfficialMatchVideoProvider> _log;

        public CompositeOfficialMatchVideoProvider(
            IEnumerable<IOfficialMatchVideoProvider> providers,
            ILogger<CompositeOfficialMatchVideoProvider> log)
        {
            // Kendini içermez (sonsuz döngü) ve öncelik sırasına dizilir.
            _providers = (providers ?? Array.Empty<IOfficialMatchVideoProvider>())
                .Where(p => p is not CompositeOfficialMatchVideoProvider)
                .OrderBy(p => p.Priority)
                .ThenBy(p => p.Name, StringComparer.Ordinal)
                .ToList();
            _log = log;
        }

        public string Name => "OfficialVideoChain";

        /// <summary>Zincirin önceliği, içindeki EN ÖNCELİKLİ sağlayıcınınkidir.</summary>
        public int Priority =>
            _providers.Count == 0
                ? OfficialVideoSourceTiers.AuxiliaryDiscovery
                : _providers.Min(p => p.Priority);

        /// <summary>
        /// Zincir, yapılandırılmış EN AZ BİR sağlayıcı varsa çalışır. Hiçbiri
        /// yapılandırılmamışsa durum dürüstçe <c>NotConfigured</c>'dır.
        /// </summary>
        public string Status =>
            _providers.Any(p => p.Status == VideoProviderStatuses.Configured)
                ? VideoProviderStatuses.Configured
                : VideoProviderStatuses.NotConfigured;

        /// <summary>
        /// SON TURUN SAĞLAYICI DÖKÜMÜ — hangi sağlayıcı çalıştı, hangisi neden atlandı.
        /// Teşhis içindir; karar akışını etkilemez.
        /// </summary>
        public IReadOnlyList<VideoProviderOutcome> LastOutcomes { get; private set; }
            = Array.Empty<VideoProviderOutcome>();

        public async Task<IReadOnlyList<OfficialVideoCandidate>> DiscoverAsync(
            VideoFixtureIdentity fixture, CancellationToken ct = default)
        {
            var outcomes = new List<VideoProviderOutcome>();
            var all = new List<OfficialVideoCandidate>();

            // Aynı video birden çok sağlayıcıdan gelebilir; İLK bulan (yani en yüksek
            // hak sahipliğine sahip yol) kazanır — sonraki kopyalar elenir.
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var p in _providers)
            {
                ct.ThrowIfCancellationRequested();

                if (p.Status != VideoProviderStatuses.Configured)
                {
                    outcomes.Add(new VideoProviderOutcome(
                        p.Name, p.Status, p.Priority, 0,
                        "yapilandirilmadi/kapali — atlandi, zincir devam etti"));
                    continue;
                }

                try
                {
                    var found = await p.DiscoverAsync(fixture, ct).ConfigureAwait(false)
                                ?? Array.Empty<OfficialVideoCandidate>();

                    var fresh = 0;
                    foreach (var c in found)
                    {
                        var key = DedupeKey(c);
                        if (!seen.Add(key)) continue;

                        // Sağlayıcı adı ve maç kimliği burada mühürlenir: aşağıdaki
                        // katmanlar adayın hangi yoldan ve hangi maç için geldiğini
                        // tahmin etmek zorunda kalmaz.
                        all.Add(c with
                        {
                            ProviderName      = c.ProviderName ?? p.Name,
                            MatchId           = c.MatchId ?? fixture.MatchId,
                            ExternalFixtureId = c.ExternalFixtureId ?? fixture.ExternalFixtureId
                        });
                        fresh++;
                    }

                    outcomes.Add(new VideoProviderOutcome(
                        p.Name, p.Status, p.Priority, fresh,
                        $"{found.Count} aday dondu, {fresh} yeni"));
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    // Kaynak tek tek çöker, zincir çökmez.
                    outcomes.Add(new VideoProviderOutcome(
                        p.Name, p.Status, p.Priority, 0, "saglayici hatasi: " + ex.GetType().Name));
                    _log.LogWarning(ex, "[POST-MATCH VIDEO] saglayici basarisiz: {Provider}", p.Name);
                }
            }

            LastOutcomes = outcomes;
            return all;
        }

        /// <summary>Aynı videonun iki kez taşınmasını önleyen anahtar.</summary>
        private static string DedupeKey(OfficialVideoCandidate c)
            => (c.Platform ?? "") + "|" + (c.ExternalVideoId ?? "");
    }
}
