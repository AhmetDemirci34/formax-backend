using System;

namespace Formax.Domain.Entities
{
    /// <summary>
    /// BİTMİŞ MAÇIN KANONİK OLAY KAYDI — gol, kart, oyuncu değişikliği, VAR.
    ///
    /// NEDEN YENİ TABLO (ölçüldü 06.09.2026):
    ///  • <c>MatchEvents</c> tablosu depoda BOŞTU (0 satır) ve dakika dışında uzatma
    ///    dakikası, asist, sağlayıcı olay kimliği gibi alanları hiç taşımıyordu.
    ///  • <c>MatchLiveEvents</c> CANLI akışın tablosudur; canlı veri kapalıyken
    ///    (LiveMatchData:Enabled=false) beslenmez ve tekilleştirme anahtarı yoktur.
    /// Bitmiş maç ekranının okuduğu kayıt, canlı yoklamaya bağımlı OLMAMALIDIR.
    ///
    /// AKIŞ: maç Finished olur → arka plan işi aday seçer → <c>fixtures/events</c>
    /// bir kez alınır → buraya yazılır. Kullanıcı maç detayını açtığında YALNIZ bu
    /// tablo okunur; tıklama başına 0 sağlayıcı isteği üretilir.
    ///
    /// TEKİLLEŞTİRME: (MatchId + ProviderEventId) benzersizdir. Sağlayıcı olay
    /// kimliği vermezse (ExternalFixtureId + dakika + tür + oyuncu) imzası kullanılır.
    /// Aynı olay iki kez yazılmaz.
    ///
    /// UYDURMA YOK: sağlayıcının vermediği alan null kalır. "Asist yok" ile "asist
    /// bilgisi gelmedi" aynı şey değildir.
    /// </summary>
    public sealed class MatchEventRecord
    {
        public long Id { get; set; }

        /// <summary>Kanonik Match.Id.</summary>
        public int MatchId { get; set; }

        /// <summary>Sağlayıcı fikstür kimliği — kaydın hangi maça ait olduğunun kanıtı.</summary>
        public string ExternalFixtureId { get; set; } = string.Empty;

        /// <summary>
        /// Sağlayıcının olay kimliği. api-football olay için ayrı bir id VERMEZ; bu alan
        /// (fikstür + dakika + tür + oyuncu) imzasından türetilen KARARLI bir anahtardır.
        /// Aynı olay ikinci kez çekilirse aynı anahtarı üretir → duplicate yazılmaz.
        /// </summary>
        public string ProviderEventId { get; set; } = string.Empty;

        /// <summary>Normal oyun dakikası (90+3 → 90).</summary>
        public int Minute { get; set; }

        /// <summary>Uzatma dakikası (90+3 → 3). Sağlayıcı vermediyse null.</summary>
        public int? ExtraMinute { get; set; }

        /// <summary>Sağlayıcı takım kimliği (ev/deplasman ayrımı için).</summary>
        public int? TeamExternalId { get; set; }

        /// <summary>Takım adı — sağlayıcının yazdığı gibi.</summary>
        public string? TeamName { get; set; }

        /// <summary>Olayın oyuncusu (golü atan, kart gören, oyundan çıkan). Yoksa null.</summary>
        public string? PlayerName { get; set; }

        /// <summary>Asist yapan / yerine giren oyuncu. Yoksa null — sıfır UYDURULMAZ.</summary>
        public string? AssistName { get; set; }

        /// <summary>Olay türü: "Goal" | "Card" | "subst" | "Var".</summary>
        public string EventType { get; set; } = string.Empty;

        /// <summary>Ayrıntı: "Normal Goal" | "Yellow Card" | "Penalty" | "Substitution 1".</summary>
        public string? Detail { get; set; }

        /// <summary>Sağlayıcının serbest açıklaması. Yoksa null.</summary>
        public string? Comments { get; set; }

        /// <summary>Verinin kaynağı ("api-football").</summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>Kaydın sağlayıcıdan alındığı an (UTC).</summary>
        public DateTime FetchedAtUtc { get; set; }
    }
}
