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

namespace Formax.Application.Services.OfficialSources
{
    /// <summary>Resmî kaynaktan tek maç olayı — FORMAX olay sözlüğüyle (Goal/Card/subst/Var).</summary>
    public sealed record OfficialMatchEvent(
        string OfficialEventId,
        int Minute,
        int? ExtraMinute,
        string Side,
        string EventType,
        string? Detail,
        string? PlayerName,
        string? AssistName);

    /// <summary>
    /// Takım istatistiği — kaynak bir alanı vermiyorsa NULL kalır (sıfır UYDURULMAZ).
    /// </summary>
    public sealed record OfficialTeamStatistics(
        int? BallPossession, int? TotalShots, int? ShotsOnTarget, int? ShotsOffTarget, int? BlockedShots,
        int? Corners, int? Offsides, int? Fouls, int? YellowCards, int? RedCards, int? GoalkeeperSaves,
        int? TotalPasses, int? AccuratePasses, int? PassAccuracy)
    {
        public bool HasAnyMeasurement =>
            BallPossession.HasValue || TotalShots.HasValue || ShotsOnTarget.HasValue || ShotsOffTarget.HasValue ||
            BlockedShots.HasValue || Corners.HasValue || Offsides.HasValue || Fouls.HasValue || YellowCards.HasValue ||
            RedCards.HasValue || GoalkeeperSaves.HasValue || TotalPasses.HasValue || AccuratePasses.HasValue || PassAccuracy.HasValue;
    }

    public sealed record OfficialMatchStatistics(OfficialTeamStatistics Home, OfficialTeamStatistics Away);

    /// <summary>Bitmiş maçın resmî olay/istatistiği — yalnız kaynağın gerçekten yayımladığı alanlar.</summary>
    public interface IOfficialPostMatchSource
    {
        string SourceKey { get; }

        Task<OfficialRead<IReadOnlyList<OfficialMatchEvent>>> ReadEventsAsync(
            OfficialMatchRecord match, OfficialRoundContext round, CancellationToken ct = default);

        /// <summary>Kaynak istatistik yayımlamıyorsa <see cref="OfficialReadOutcomes.NotSupported"/>.</summary>
        Task<OfficialRead<OfficialMatchStatistics>> ReadStatisticsAsync(
            OfficialMatchRecord match, OfficialRoundContext round, CancellationToken ct = default);
    }

    /// <summary>
    /// TEK MAÇ SAYFASI (TELAFİ YOLU) — maç listesi yalnız güncel haftayı/turu yayımlayan kaynaklarda (TFF)
    /// hafta döndükten sonra kaçırılmış maç listede bulunmaz. Bu arayüz, kaynağın kendi maç sayfasını
    /// KAYITLI resmî maç kimliğiyle okur; kimlik tahmin edilmez, gezinme yapılmaz, tek GET'tir.
    /// Sonuç botu bunu yalnız (1) liste okuması başarılıyken maç listede bulunamadığında ve
    /// (2) başlama saatinden <see cref="OfficialMatchPagePolicy.RecoveryAfter"/> geçtikten sonra dener.
    /// </summary>
    public interface IOfficialMatchPageSource
    {
        string SourceKey { get; }

        /// <summary>Kaynağın maç sayfasından tek kayıt; sayfa skor yayımlamadıysa değer doludur ama durum final değildir.</summary>
        Task<OfficialRead<OfficialMatchRecord>> ReadMatchAsync(
            string officialMatchId, OfficialRoundContext round, CancellationToken ct = default);
    }

    /// <summary>Maç sayfası telafi yolunun tek ayarı.</summary>
    public static class OfficialMatchPagePolicy
    {
        /// <summary>Başlama saatinden bu süre geçmeden telafi okuması yapılmaz (sıcak yolda liste kullanılır).</summary>
        public static readonly System.TimeSpan RecoveryAfter = System.TimeSpan.FromHours(6);
    }

    /// <summary>
    /// SONUÇ TEYİDİ — maç listesi "bitti" bayrağı taşımayan kaynakta (TFF) skor, aynı resmî
    /// kaynağın maç sayfasıyla karşılaştırılır; uyuşmazsa sonuç kesinleştirilmez.
    /// </summary>
    public interface IOfficialResultConfirmation
    {
        Task<OfficialRead<(int Home, int Away)?>> ConfirmScoreAsync(
            OfficialMatchRecord match, OfficialRoundContext round, CancellationToken ct = default);
    }
}
