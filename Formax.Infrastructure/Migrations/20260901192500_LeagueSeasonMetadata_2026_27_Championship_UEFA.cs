using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <summary>
    /// KİLİTLİ KAPSAMIN KALAN 4 ORGANİZASYONU — 2026/27 SEZON METADATA KAYITLARI.
    ///
    /// Kilitli kapsam 11 organizasyondur; metadata yalnız 7'si için vardı. Bu migration
    /// eksik dördü tamamlar: 40 Championship, 2 UCL, 3 UEL, 848 UECL. Mevcut 7 kayda
    /// DOKUNMAZ (Upsert IF NOT EXISTS ile yazar).
    ///
    /// KURAL: kaynak URL'si ve doğrulama tarihi olmayan hiçbir kayıt eklenmez. Tarihler
    /// uygulama koduna GİRMEZ; yalnız bu metadata tablosunda yaşar. Arama sonucu özeti
    /// ya da üçüncü taraf site KAYNAK SAYILMAZ — aşağıdaki tarihlerin hepsi resmî
    /// efl.com / uefa.com sayfalarından OKUNARAK alınmıştır.
    ///
    /// UEFA'DA SEZON BAŞLANGICI = O SEZONUN İLK ELEME TURUNUN İLK MAÇ GÜNÜ.
    /// Lig aşamasının başlangıcı DEĞİLDİR: eleme ve play-off turları aynı organizasyon
    /// sezonuna dahildir ve api-football'da aynı lig id'si altında gelir (2/3/848).
    /// Başlangıç lig aşamasına çekilseydi Temmuz–Ağustos'taki bütün eleme maçları sezon
    /// penceresinin DIŞINDA kalır ve sonuç/tamlık hesabına hiç girmezdi.
    ///
    /// EndUtc penceresi DIŞLAYICIDIR (mevcut kayıtlarla aynı gelenek: Premier League'de
    /// son hafta 30.05.2027, EndUtc 31.05.2027). Bu yüzden son maç günü + 1 yazılır.
    ///
    /// DOĞRULAMA İZİ: her StartUtc, depodaki GERÇEK sağlayıcı fikstürünün o ligdeki en
    /// erken 2026/27 maçıyla karşılaştırılıp tutturulmuştur (UCL 07.07 16:00,
    /// UEL 09.07 16:00, UECL 07.07 18:00, Championship 14.08 19:00 Wolves–Blackburn).
    /// </summary>
    public partial class LeagueSeasonMetadata_2026_27_Championship_UEFA : Migration
    {
        private const string VerifiedAt = "2026-09-01T00:00:00";

        /// <summary>
        /// IF NOT EXISTS ile yazar: kayıt elle girilmiş olabilir ve migration iki kez
        /// çalışsa da çakışmaz. Mevcut doğrulanmış kayıtların ÜZERİNE YAZMAZ.
        /// </summary>
        private static void Upsert(
            MigrationBuilder b, int leagueId, string startUtc, string endUtc, string source, string notes,
            string verificationStatus = "Confirmed")
        {
            var end = endUtc == null ? "NULL" : $"'{endUtc}'";
            b.Sql($@"
IF NOT EXISTS (SELECT 1 FROM [LeagueSeasons] WHERE [LeagueId] = {leagueId} AND [SeasonYear] = 2026)
INSERT INTO [LeagueSeasons] ([LeagueId],[SeasonYear],[StartUtc],[EndUtc],[Source],[VerifiedAtUtc],[Notes],[VerificationStatus])
VALUES ({leagueId}, 2026, '{startUtc}', {end}, N'{source}', '{VerifiedAt}', N'{notes.Replace("'", "''")}', N'{verificationStatus}');");
        }

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 40 — EFL Championship ────────────────────────────────────────────
            // efl.com: "SKY BET CHAMPIONSHIP, LEAGUE ONE AND LEAGUE TWO 14-16 August 2026"
            // ve Key Dates: "Opening weekend: 14 - 16 August 2026",
            //               "Closing weekend: Championship - Saturday 1 May 2027".
            // Açılış turunun EN ERKEN maç günü alınır (mevcut Serie A / Ligue 1 geleneği).
            // EndUtc = kapanış haftası (01.05.2027) + 1 gün.
            // EndUtc NULL: Championship play-off final tarihi resmi kaynakta YAYIMLANMAMISTI.
            // Kapanis haftasindan (1 May 2027) tahmini bir sezon bitisi TURETILMEZ; sezon
            // cozumu EndUtc null iken kova sinirini kullanir ve bozulmaz.
            Upsert(migrationBuilder, 40, "2026-08-14T00:00:00", null,
                "Verified:OfficialLeague",
                "efl.com/competitions/key-dates/ + efl.com/news/2026/january/12/" +
                "efl-kick-off-dates-confirmed-for-2026-27-season/ (opening weekend 14-16 Aug 2026; " +
                "Championship closing weekend Sat 1 May 2027) | play-off finali resmi kaynakta " +
                "yayimlanmamis -> EndUtc NULL, tarih TURETILMEDI | dogrulama 2026-09-01",
                "PendingOfficialConfirmation");

            // ── 2 — UEFA Champions League ────────────────────────────────────────
            // uefa.com eleme sayfasi: "qualifying for the 2026/27 season started on 7 July";
            // "First qualifying round: 7/8 & 14/15 July 2026". On eleme turu YOKTU.
            // uefa.com 2026/27 sayfasi: "kicked off on 7 July 2026 and concludes with the
            // final at Estadio Metropolitano in Madrid on Saturday 5 June 2027".
            Upsert(migrationBuilder, 2, "2026-07-07T00:00:00", "2027-06-06T00:00:00",
                "Verified:UEFA",
                "uefa.com/uefachampionsleague/news/02a6-20e5a8be4e63-ae971c582f8c-1000 (1. eleme turu " +
                "7/8 & 14/15 Jul 2026) + uefa.com/uefachampionsleague/news/02a6-20d57cfcd03e-" +
                "407c22a7f465-1000 (kicked off 7 Jul 2026; final 5 Jun 2027) | eleme+play-off ayni " +
                "sezona dahil | dogrulama 2026-09-01");

            // ── 3 — UEFA Europa League ───────────────────────────────────────────
            // uefa.com eleme sayfasi: "ran from 9 July to 27 August";
            // "First qualifying round: 9 & 16 July 2026".
            // uefa.com 2026/27 sayfasi: "kicked off on 9 July 2026 and runs until the final
            // in Frankfurt on 26 May 2027".
            Upsert(migrationBuilder, 3, "2026-07-09T00:00:00", "2027-05-27T00:00:00",
                "Verified:UEFA",
                "uefa.com/uefaeuropaleague/news/02a6-20e5db0029dd-8241a8d00925-1000 (1. eleme turu " +
                "9 & 16 Jul 2026) + uefa.com/uefaeuropaleague/news/02a6-20d57d095740-" +
                "e1e0b3de85df-1000 (kicked off 9 Jul 2026; final Frankfurt 26 May 2027) | eleme+play-off " +
                "ayni sezona dahil | dogrulama 2026-09-01");

            // ── 848 — UEFA Conference League ─────────────────────────────────────
            // KAYNAK CELISKISI (bilerek kayda gecirildi): uefa.com genel 2026/27 sayfasi
            // "kicked off on 9 July 2026" diyor; ayni sitedeki ELEME sayfasi ise
            // "kicked off on 7 July" + "First qualifying round: 7-9 & 14-16 July 2026" diyor.
            // Kural "ilk eleme turunun ILK MAC GUNU" oldugu icin 7 Temmuz alindi; depodaki
            // gercek saglayici fiksturu de en erken maci 07.07.2026 18:00 olarak dogruluyor.
            Upsert(migrationBuilder, 848, "2026-07-07T00:00:00", "2027-06-03T00:00:00",
                "Verified:UEFA",
                "uefa.com/uefaconferenceleague/news/02a6-20e5e911587f-cc10425958b3-1000 (1. eleme turu " +
                "7-9 & 14-16 Jul 2026) + uefa.com/uefaconferenceleague/news/02a6-20d57d15f093-" +
                "a90cf54c928f-1000 (final Istanbul 2 Jun 2027) | genel sayfa '9 Jul' der; eleme sayfasi " +
                "ve gercek fikstur '7 Jul' -> 7 Jul alindi | dogrulama 2026-09-01");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DELETE FROM [LeagueSeasons] WHERE [SeasonYear] = 2026 AND [LeagueId] IN (40, 2, 3, 848);");
        }
    }
}
