using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Formax.Application.Services.OfficialSources
{
    /// <summary>
    /// RESMÎ İÇERİK İNDİRİCİ — dış kaynağa çıkan TEK kapı.
    ///
    /// Sözleşme: yalnız HTTPS + izinli host; özel/yerel IP'ye çıkmaz; her yönlendirmede
    /// host yeniden doğrulanır; zaman aşımı ve gövde boyutu sınırı vardır; host başına hız
    /// sınırı uygular; ETag/Last-Modified ile koşullu GET yapar; kalıcı önbellek ve defter
    /// tutar; aynı tur aynı adresi bir kez indirir. Script ÇALIŞTIRMAZ, gövde yalnız metindir.
    /// Kullanıcı isteği yolunda (maç detayı, sonuçlar) bu arayüz ÇÖZÜLMEZ.
    /// </summary>
    public interface IOfficialContentFetcher
    {
        Task<OfficialFetchResult> FetchAsync(OfficialFetchRequest request, CancellationToken ct = default);

        /// <summary>İşleyen bu içeriği başarıyla tükettiğinde çağırır (aynı özet yeniden işlenmez).</summary>
        Task MarkProcessedAsync(string url, string contentHash, CancellationToken ct = default);

        /// <summary>Defter satırına aday/kabul sayısını ve kararı yazar.</summary>
        Task RecordDecisionAsync(long ledgerId, int candidates, int accepted, string decision, CancellationToken ct = default);
    }

    /// <summary>
    /// TEK RESMÎ MÜSABAKA KAYNAĞI — kaynak türüne özel parser sözleşmesi.
    /// Maç listesi ortak akıştan BİR KEZ okunur; kadro maç başına okunur.
    /// </summary>
    public interface IOfficialCompetitionSource
    {
        string SourceKey { get; }

        /// <summary>Kaynağın gerçekten desteklediği amaçlar (kayıt defteriyle aynı olmalı).</summary>
        IReadOnlyCollection<string> Purposes { get; }

        /// <summary>Kaynağın maç listesi (kimlik + durum + skor).</summary>
        Task<OfficialRead<IReadOnlyList<OfficialMatchRecord>>> ReadMatchesAsync(
            OfficialRoundContext round, CancellationToken ct = default);

        /// <summary>
        /// Tek maçın resmî kadrosu. Değer null ve sonuç Ok ise kaynak GEÇERLİ cevap verdi ama
        /// kadroyu henüz yayımlamadı (başarı DEĞİL; sonraki kontrol açık kalır).
        /// </summary>
        Task<OfficialRead<OfficialLineupDocument>> ReadLineupAsync(
            OfficialMatchRecord match, OfficialRoundContext round, CancellationToken ct = default);
    }
}
