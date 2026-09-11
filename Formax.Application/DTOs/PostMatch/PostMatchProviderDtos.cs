using System.Collections.Generic;

namespace Formax.Application.DTOs.PostMatch
{
    /// <summary>
    /// SAĞLAYICIDAN GELEN TEK OLAY — <c>fixtures/events</c> yanıtının birebir karşılığı.
    ///
    /// Alanlar sağlayıcıda yoksa null gelir; çağıran bunları SIFIRA ÇEVİRMEZ.
    /// </summary>
    public sealed class SportsMatchEvent
    {
        public int Minute { get; set; }
        public int? ExtraMinute { get; set; }
        public int? TeamExternalId { get; set; }
        public string? TeamName { get; set; }
        public string? PlayerName { get; set; }
        public string? AssistName { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string? Detail { get; set; }
        public string? Comments { get; set; }
    }

    /// <summary>
    /// SAĞLAYICIDAN GELEN TEK TAKIM İSTATİSTİĞİ — <c>fixtures/statistics</c> karşılığı.
    ///
    /// HER ÖLÇÜM NULLABLE'dır. Sağlayıcının listede hiç göndermediği bir tür için
    /// 0 yazmak, olmayan veriyi var etmektir; bu sınıf bunu YAPMAZ.
    /// </summary>
    public sealed class SportsTeamMatchStatistics
    {
        public int? TeamExternalId { get; set; }
        public string? TeamName { get; set; }

        public int? BallPossession { get; set; }
        public int? TotalShots { get; set; }
        public int? ShotsOnTarget { get; set; }
        public int? ShotsOffTarget { get; set; }
        public int? BlockedShots { get; set; }
        public int? Corners { get; set; }
        public int? Offsides { get; set; }
        public int? Fouls { get; set; }
        public int? YellowCards { get; set; }
        public int? RedCards { get; set; }
        public int? GoalkeeperSaves { get; set; }
        public int? TotalPasses { get; set; }
        /// <summary>Başarılı pas SAYISI ("Passes accurate") — sağlayıcının gerçekten gönderdiği alan.</summary>
        public int? AccuratePasses { get; set; }
        public int? PassAccuracy { get; set; }

        /// <summary>En az bir ölçüm dolu mu? Boşsa "istatistik geldi" SAYILMAZ.</summary>
        public bool HasAnyMeasurement =>
            BallPossession.HasValue || TotalShots.HasValue || ShotsOnTarget.HasValue
            || ShotsOffTarget.HasValue || BlockedShots.HasValue || Corners.HasValue
            || Offsides.HasValue || Fouls.HasValue || YellowCards.HasValue
            || RedCards.HasValue || GoalkeeperSaves.HasValue || TotalPasses.HasValue
            || AccuratePasses.HasValue || PassAccuracy.HasValue;
    }

    /// <summary>
    /// Bir fikstürün istatistik yanıtı: sağlayıcı sırasına göre ev (0) ve deplasman (1).
    ///
    /// <see cref="Succeeded"/> false iken çağıran HİÇBİR ŞEY YAZMAZ: sağlayıcı hatası
    /// ile "bu maçta istatistik yok" birbirine karıştırılamaz. Karıştırılırsa bir
    /// daha hiç denenmez ve veri kalıcı olarak kaybolur.
    /// </summary>
    public sealed class SportsMatchStatisticsResult
    {
        public bool Succeeded { get; set; }
        public List<SportsTeamMatchStatistics> Teams { get; set; } = new();

        /// <summary>En az bir takımda en az bir gerçek ölçüm var mı?</summary>
        public bool HasRealData => Succeeded && Teams.Exists(t => t.HasAnyMeasurement);

        public static SportsMatchStatisticsResult Failed() => new() { Succeeded = false };
    }

    /// <summary>
    /// Bir fikstürün olay yanıtı. <see cref="Succeeded"/> ayrımı istatistikteki ile aynı
    /// gerekçeyle vardır: boş liste "olay yok" olabilir, sağlayıcı hatası ise DEĞİLDİR.
    /// </summary>
    public sealed class SportsMatchEventsResult
    {
        public bool Succeeded { get; set; }
        public List<SportsMatchEvent> Events { get; set; } = new();

        public static SportsMatchEventsResult Failed() => new() { Succeeded = false };
    }
}
