using System;

namespace Formax.Domain.Entities
{
    /// <summary>
    /// BİTMİŞ MAÇIN KANONİK TAKIM İSTATİSTİĞİ — maç başına iki satır (ev + deplasman).
    ///
    /// NEDEN YENİ TABLO (ölçüldü 06.09.2026): mevcut <c>MatchLiveStats</c> tablosunda
    /// 87.546 satırın 87.502'si SKOR DIŞINDA tamamen sıfırdı. Yani "veri var" gibi
    /// görünen satırların %99,9'u aslında BOŞTU: canlı yoklama kapalıyken sonuç
    /// yazıcısı skoru koyuyor, istatistik alanlarını sıfır bırakıyordu. Alanların
    /// hepsi <c>int</c> olduğu için "0 korner" ile "korner bilgisi yok" ayırt
    /// EDİLEMİYORDU — ekran, olmayan veriyi "0" diye gösterirdi.
    ///
    /// Bu tabloda her ölçüm NULLABLE'dır. Sağlayıcı bir alanı vermediyse null kalır;
    /// sıfır UYDURULMAZ. <see cref="HasAnyMeasurement"/> "gerçekten istatistik var mı?"
    /// sorusunun TEK cevabıdır — skor bu karara girmez.
    ///
    /// AKIŞ: maç Finished → arka plan işi <c>fixtures/statistics</c>'i bir kez alır →
    /// buraya yazar. Kullanıcı maç detayını açtığında yalnız bu tablo okunur.
    /// </summary>
    public sealed class MatchTeamStatistic
    {
        public long Id { get; set; }

        /// <summary>Kanonik Match.Id.</summary>
        public int MatchId { get; set; }

        /// <summary>Sağlayıcı fikstür kimliği.</summary>
        public string ExternalFixtureId { get; set; } = string.Empty;

        /// <summary>"Home" | "Away" — MatchId ile birlikte benzersizdir.</summary>
        public string Side { get; set; } = string.Empty;

        /// <summary>Sağlayıcı takım kimliği.</summary>
        public int? TeamExternalId { get; set; }

        /// <summary>Takım adı — sağlayıcının yazdığı gibi.</summary>
        public string? TeamName { get; set; }

        // ── ÖLÇÜMLER — hepsi NULLABLE. null = "sağlayıcı vermedi", 0 = "gerçekten sıfır"
        /// <summary>Topa sahip olma yüzdesi (0-100).</summary>
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

        /// <summary>
        /// BAŞARILI PAS SAYISI — sağlayıcının <c>"Passes accurate"</c> alanı.
        ///
        /// ÖLÇÜLDÜ (06.09.2026): api-football bu plan/liglerde <c>"Passes %"</c> ALANINI
        /// HİÇ GÖNDERMİYOR; gönderdiği tek pas-başarısı verisi bu SAYIDIR. Yalnız yüzde
        /// alanı eşlenseydi 10 satırın 10'unda da null kalır ve gerçek veri kaybolurdu.
        /// Yüzde, <see cref="TotalPasses"/> ile birlikte hesaplanabilir; burada ham
        /// sağlayıcı değeri saklanır — türetilmiş sayı depoya YAZILMAZ.
        /// </summary>
        public int? AccuratePasses { get; set; }

        /// <summary>
        /// Başarılı pas yüzdesi (0-100) — sağlayıcı <c>"Passes %"</c> gönderirse dolar.
        /// Göndermezse null kalır ve <see cref="AccuratePasses"/> kullanılır.
        /// </summary>
        public int? PassAccuracy { get; set; }

        public string Source { get; set; } = string.Empty;
        public DateTime FetchedAtUtc { get; set; }

        /// <summary>
        /// GERÇEK İSTATİSTİK VAR MI? — "HasStatistics" kararının TEK yeri.
        ///
        /// Tek bir ölçümün bile dolu olması yeterlidir; hiçbiri dolu değilse satır
        /// yazılmış olsa dahi istatistik YOKTUR. Boş sağlayıcı cevabı, "bu maçta her
        /// şey sıfırdı" ANLAMINA GELMEZ.
        /// </summary>
        public bool HasAnyMeasurement =>
            BallPossession.HasValue || TotalShots.HasValue || ShotsOnTarget.HasValue
            || ShotsOffTarget.HasValue || BlockedShots.HasValue || Corners.HasValue
            || Offsides.HasValue || Fouls.HasValue || YellowCards.HasValue
            || RedCards.HasValue || GoalkeeperSaves.HasValue || TotalPasses.HasValue
            || AccuratePasses.HasValue || PassAccuracy.HasValue;
    }
}
