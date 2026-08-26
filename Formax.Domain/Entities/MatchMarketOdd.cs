using System;

namespace Formax.Domain.Entities
{
    /// <summary>
    /// GERÇEK market oranı — bir maçın TEK bir marketi için sağlayıcıdan (api-football /odds)
    /// alınmış son okuma. Sentetik/örnek değer ASLA yazılmaz; satır varsa sağlayıcıdan gelmiştir.
    ///
    /// Mevcut <see cref="OddsSnapshot"/> yalnız 1X2 taşır (Radar hareket motorunun girdisi) ve
    /// KG / Alt-Üst / Çifte Şans / İlk Yarı marketlerini ifade edemez. Bu tablo market-başına
    /// tek satır tutar: (MatchId, MarketKey) benzersizdir; yeni okuma aynı satırı günceller.
    /// </summary>
    public sealed class MatchMarketOdd
    {
        public long Id { get; set; }

        /// <summary>Formax Match.Id.</summary>
        public int MatchId { get; set; }

        /// <summary>Normalize market anahtarı — <see cref="Formax.Domain.Constants.OddsMarketKeys"/>.</summary>
        public string MarketKey { get; set; } = string.Empty;

        /// <summary>Ondalık oran (ör. 1.83). Sağlayıcı değeri; hesaplanmaz.</summary>
        public decimal Odd { get; set; }

        /// <summary>Bu oranın alındığı bahis sağlayıcısı (şeffaflık; UI göstermek zorunda değil).</summary>
        public string BookmakerName { get; set; } = string.Empty;

        public int BookmakerId { get; set; }

        /// <summary>Bir önceki okuma (hareket yönü için). İlk yazımda null.</summary>
        public decimal? PreviousOdd { get; set; }

        public DateTime CapturedAtUtc { get; set; }
    }
}
