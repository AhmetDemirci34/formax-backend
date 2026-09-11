using System;
using System.Collections.Generic;

namespace Formax.Application.Services.PostMatch
{
    /// <summary>
    /// RESMÎ VİDEO ARAMASININ TEKRAR TAKVİMİ — tek kaynak.
    ///
    /// Son düdükten sonra DÖRT deneme: FT+60dk → FT+3sa → FT+6sa → FT+24sa. Aralıklar
    /// bir önceki GERÇEK denemenin üzerine eklenir (60dk + 2sa = FT+3sa, + 3sa = FT+6sa,
    /// + 18sa = FT+24sa).
    ///
    /// NEDEN APPLICATION KATMANINDA: aynı takvimi iki taraf okur — arka plan işi
    /// ("şimdi denemeli miyim?") ve maç detayı ("arama sürüyor mu, bitti mi?"). Takvim
    /// iki yerde yazılırsa ekran, işin hiç yapmadığı bir denemeyi "yapıldı" sanar.
    /// </summary>
    public static class PostMatchVideoSchedule
    {
        /// <summary>Maç bitişinden ilk bakışa kadar geçen süre.</summary>
        public static readonly TimeSpan FirstCheckAfterFullTime = TimeSpan.FromMinutes(60);

        /// <summary>İlk bakış boş dönerse sırasıyla beklenecek süreler.</summary>
        public static readonly IReadOnlyList<TimeSpan> RetryBackoff = new[]
        {
            TimeSpan.FromHours(2),
            TimeSpan.FromHours(3),
            TimeSpan.FromHours(18)
        };

        /// <summary>Toplam deneme hakkı: ilk bakış + üç tekrar.</summary>
        public static int MaxAttempts => 1 + RetryBackoff.Count;

        /// <summary>
        /// Şimdi denenmeli mi? Saf fonksiyondur.
        /// </summary>
        /// <param name="attemptsSoFar">Kalıcı defterdeki GERÇEK deneme sayısı.</param>
        /// <param name="lastAttemptUtc">En son deneme anı; hiç denenmediyse null.</param>
        public static bool IsDue(DateTime matchEndUtc, int attemptsSoFar, DateTime? lastAttemptUtc, DateTime nowUtc)
        {
            if (attemptsSoFar >= MaxAttempts) return false;                 // hak bitti
            if (attemptsSoFar <= 0) return nowUtc >= matchEndUtc + FirstCheckAfterFullTime;
            if (lastAttemptUtc == null) return true;                        // sayaç var, an yok → dene
            return nowUtc >= lastAttemptUtc.Value + RetryBackoff[Math.Min(attemptsSoFar - 1, RetryBackoff.Count - 1)];
        }
    }

    /// <summary>
    /// Kalıcı defterin bir fikstür + amaç için özeti.
    /// </summary>
    /// <param name="Attempts">GERÇEK deneme sayısı (engellenen denemeler geri alınmıştır).</param>
    /// <param name="LastAttemptUtc">En son deneme anı.</param>
    /// <param name="LastOutcome">En yeni satırın son sonucu ("Applied", "NoData", "Unavailable", "Blocked:…").</param>
    /// <param name="ExhaustedRecorded">
    /// Son deneme "hak bitti, resmî video bulunamadı" (<c>Unavailable</c>) olarak kayda geçti mi?
    /// </param>
    public sealed record FixtureAttemptSummary(
        int Attempts,
        DateTime? LastAttemptUtc,
        string? LastOutcome,
        bool ExhaustedRecorded)
    {
        public static readonly FixtureAttemptSummary None = new(0, null, null, false);
    }

    /// <summary>
    /// MAÇ ÖZETİ VİDEOSU ARAMA DURUMU — ekranın hangi cümleyi kuracağının TEK kuralı.
    ///
    /// KURAL KAYNAĞI KALICI DEFTERDİR, SAAT DEĞİL (07.09.2026 ürün kararı):
    /// ekran eskiden "maç biteli 26 saat geçti mi?" diye bakıyordu. Oysa iş hiç
    /// çalışmamış olabilir — o zaman "bulunamadı" demek, yapılmamış bir aramanın
    /// sonucunu uydurmaktır. Artık:
    ///
    ///  • Oynatılabilir resmî video varsa → <see cref="Found"/> (player). Sonraki denemeler iptal.
    ///  • Dört GERÇEK deneme defterde tamamlandı VE son deneme "bulunamadı" olarak kayda
    ///    geçti → <see cref="NotFound"/>.
    ///  • Aksi her durumda → <see cref="Checking"/>: en az bir deneme hakkı vardır ya da
    ///    takvim geçmiş ama iş henüz çalışmamıştır.
    ///
    /// Plan/rate limit engeli defterde deneme SAYILMAZ (bkz. <c>RecordFixtureAttemptBlocked</c>);
    /// bu yüzden engellenen bir tur kullanıcıyı erken "bulunamadı"ya götüremez.
    /// </summary>
    public static class PostMatchVideoSearchStatus
    {
        public const string Found = "Found";
        public const string Checking = "Checking";
        public const string NotFound = "NotFound";

        public static string Resolve(bool hasPlayableVideo, FixtureAttemptSummary ledger)
        {
            if (hasPlayableVideo) return Found;
            ledger ??= FixtureAttemptSummary.None;

            return ledger.Attempts >= PostMatchVideoSchedule.MaxAttempts && ledger.ExhaustedRecorded
                ? NotFound
                : Checking;
        }
    }
}
