using System;
using System.Globalization;
using Formax.Application.DTOs.PostMatch;

namespace Formax.Application.Services.PostMatch
{
    /// <summary>
    /// OLAY TEKİLLEŞTİRME ANAHTARI — "bu olayı daha önce yazdık mı?" sorusunun tek cevabı.
    ///
    /// PROBLEM: api-football <c>fixtures/events</c> yanıtında olay başına bir kimlik
    /// VERMEZ. Kimlik olmadan aynı maç iki kez işlendiğinde (yeniden deneme, ikinci
    /// job turu, elle tetik) bütün olaylar ikinci kez yazılırdı; ekranda her gol iki
    /// kez görünürdü.
    ///
    /// ÇÖZÜM: olayın KENDİSİNDEN kararlı bir anahtar türetmek. Aynı olay, aynı yanıtta
    /// kaçıncı kez işlenirse işlensin AYNI anahtarı üretir; DB'deki benzersiz indeks
    /// ikinci yazımı reddeder.
    ///
    /// Anahtara giren alanlar bilerek dardır ve hepsi sağlayıcının değiştirmediği
    /// alanlardır: fikstür, dakika+uzatma, tür, ayrıntı, takım, oyuncu. Yorum
    /// (<c>comments</c>) anahtara GİRMEZ — sağlayıcı onu sonradan doldurabilir ve
    /// aynı olay yeni bir kayıt gibi görünürdü.
    /// </summary>
    public static class PostMatchEventKey
    {
        /// <summary>Kararlı anahtar. Aynı olay → aynı metin, her zaman.</summary>
        public static string Build(string externalFixtureId, SportsMatchEvent e)
        {
            var minute = e.Minute.ToString(CultureInfo.InvariantCulture);
            var extra = e.ExtraMinute?.ToString(CultureInfo.InvariantCulture) ?? "-";
            var team = e.TeamExternalId?.ToString(CultureInfo.InvariantCulture)
                       ?? Normalize(e.TeamName);
            var type = Normalize(e.EventType);
            var detail = Normalize(e.Detail);
            var player = Normalize(e.PlayerName);

            return $"{externalFixtureId}|{minute}+{extra}|{type}|{detail}|{team}|{player}";
        }

        /// <summary>
        /// Anahtar bileşeni normalizasyonu — boşluk ve büyük/küçük harf farkı anahtarı
        /// DEĞİŞTİRMEZ. Türkçe İ/ı tuzağına düşmemek için <c>ToLowerInvariant</c>
        /// kullanılmaz; anahtar kültürden bağımsız olmalıdır.
        /// </summary>
        private static string Normalize(string? s)
            => string.IsNullOrWhiteSpace(s)
                ? "-"
                : s.Trim().ToUpperInvariant();
    }
}
