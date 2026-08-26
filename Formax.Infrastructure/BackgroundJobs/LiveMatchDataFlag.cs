using Microsoft.Extensions.Configuration;

namespace Formax.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// CANLI MAÇ VERİSİ ana anahtarı — <c>LiveMatchData:Enabled</c>.
    ///
    /// Canlı skor/dakika/olay verisini DIŞ KAYNAKTAN SÜREKLİ ÇEKEN tüm job'lar bu tek bayrağa
    /// bakar. Kapalıyken (varsayılan) hiçbir canlı yoklama başlamaz; kod, tablolar, DTO'lar ve
    /// endpoint'ler olduğu gibi durur — bayrak true yapılınca özellik aynen geri gelir.
    ///
    /// Neden kapalı: canlı yoklama api-football günlük kotasının (7.500) büyük kısmını
    /// tüketiyordu — tek başına <c>/fixtures?live=all</c> 30 sn'de bir, günde ~2.880 istek.
    ///
    /// KAPSAM: yalnız CANLI veri. Maç öncesi akış (fikstür, kadro, sakatlık, standings, takım
    /// istatistiği, haber, AI/probability/confidence) bu bayraktan ETKİLENMEZ.
    /// </summary>
    public static class LiveMatchDataFlag
    {
        public const string ConfigKey = "LiveMatchData:Enabled";

        /// <summary>Varsayılan KAPALI: config'te açıkça true yazmadıkça canlı yoklama başlamaz.</summary>
        public static bool IsEnabled(IConfiguration config)
            => config.GetValue(ConfigKey, false);
    }
}
