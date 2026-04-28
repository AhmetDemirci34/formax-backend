using System;
using System.Threading.Tasks;

namespace Formax.Application.Interfaces
{
    /// <summary>
    /// Canlı veri sağlayıcı (odds/medya/oynanma yoğunluğu).
    /// v1.2: amaç çoğunluk yoğunluğunu (0–100) üretmek.
    /// Bu arayüz, gerçek sağlayıcı entegrasyonu için kapıdır.
    /// </summary>
    public interface IOynanmaSinyalProvider
    {
        Task<OynanmaSinyalleri?> GetAsync(int matchId);
    }

    public sealed class OynanmaSinyalleri
    {
        /// <summary>0–100: çoğunluğun bir tarafa yoğunlaşma şiddeti.</summary>
        public int Intensity { get; set; }

        /// <summary>Home / Away / Denge (FORMAX dil kimliği: None yok)</summary>
        public string Side { get; set; } = "Denge";

        /// <summary>0–100 (opsiyonel): oran hareketi şiddeti</summary>
        public int OddsMove { get; set; }

        /// <summary>0–100 (opsiyonel): medya eğilimi</summary>
        public int MediaTrend { get; set; }

        /// <summary>
        /// Snapshot'ın en son güncellendiği zaman (UTC).
        /// Freshness (tazelik) kuralları bu alana göre çalışır.
        /// </summary>
        public DateTime LastUpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
