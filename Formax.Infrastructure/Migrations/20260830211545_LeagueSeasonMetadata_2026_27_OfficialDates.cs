using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <summary>
    /// 2026/27 SEZON METADATA KAYITLARI — RESMÎ LİG/FEDERASYON KAYNAKLARINDAN.
    ///
    /// KURAL: kaynak URL'si ve doğrulama tarihi olmayan hiçbir kayıt eklenmez. Tarihler
    /// uygulama koduna GİRMEZ; yalnız bu metadata tablosunda LeagueId + SeasonYear olarak
    /// yaşar (kod tablodan okur).
    ///
    /// StartUtc = ligin AÇILIŞ TURUNUN ilk günü. Resmî duyuru "hafta sonu" olarak
    /// yazıldığında (Serie A, Ligue 1) o turun EN ERKEN maç günü alınır; böylece açılış
    /// turunun hiçbir maçı sezon penceresinin dışında kalmaz. Her satırın Notes alanında
    /// resmî kaynağın URL'si ve doğrulama tarihi vardır.
    ///
    /// EKLENMEYENLER (bilerek):
    ///  • 40 Championship — efl.com içeriği çıkarılamadı; resmî tarih DOĞRULANAMADI.
    ///  • 2 / 3 / 848 UEFA — bu id'ler altında hem Temmuz'daki ELEME turları hem Eylül'de
    ///    başlayan lig aşaması var. "Sezon başlangıcı" hangisi sayılacak ürün kararıdır;
    ///    karar verilmeden kayıt girilmez.
    /// </summary>
    public partial class LeagueSeasonMetadata_2026_27_OfficialDates : Migration
    {
        private const string Src = "Verified:OfficialLeague";
        private const string VerifiedAt = "2026-08-31T00:00:00";

        /// <summary>
        /// IF NOT EXISTS ile yazılır: kayıt elle girilmiş olabilir (çalışan bir örnek
        /// derlemeyi kilitlediğinde metadata SQL ile eklendi). Migration her ortamda
        /// aynı sonucu üretir ve iki kez çalışsa da çakışmaz.
        /// </summary>
        private static void Upsert(MigrationBuilder b, int leagueId, string startUtc, string endUtc, string notes)
        {
            var end = endUtc == null ? "NULL" : $"'{endUtc}'";
            b.Sql($@"
IF NOT EXISTS (SELECT 1 FROM [LeagueSeasons] WHERE [LeagueId] = {leagueId} AND [SeasonYear] = 2026)
INSERT INTO [LeagueSeasons] ([LeagueId],[SeasonYear],[StartUtc],[EndUtc],[Source],[VerifiedAtUtc],[Notes])
VALUES ({leagueId}, 2026, '{startUtc}', {end}, N'{Src}', '{VerifiedAt}', N'{notes.Replace("'", "''")}');");
        }

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // İngiltere — Premier League: "opening match round ... Friday 21 August 2026",
            // son hafta "Sunday 30 May 2027".
            Upsert(migrationBuilder, 39, "2026-08-21T00:00:00", "2027-05-31T00:00:00",
                "premierleague.com/en/news/4468487/dates-for-202627-premier-league-season-confirmed | dogrulama 2026-08-31");

            // İtalya — Serie A: "iniziera il weekend del 23 agosto 2026 e terminera il
            // 30 maggio 2027"; acilis turunun ilk mac gunu 22 Agustos.
            Upsert(migrationBuilder, 135, "2026-08-22T00:00:00", "2027-05-31T00:00:00",
                "legaseriea.it/serie-a/news/le-date-della-stagione-2026-2027 (weekend del 23 agosto; acilis turu 22.08) | dogrulama 2026-08-31");

            // Almanya — Bundesliga: DFL "Bundesliga-Auftakt am 28. August 2026".
            Upsert(migrationBuilder, 78, "2026-08-28T00:00:00", null,
                "dfl.de/de/aktuelles/rahmenterminkalender-fuer-die-saison-2026-27-bundesliga-auftakt-am-28-august-2-bundesliga-startet-am-7-august/ | dogrulama 2026-08-31");

            // Fransa — Ligue 1: "week-end du 23 aout 2026", sezon sonu "samedi 29 mai 2027";
            // acilis turu Cuma 21 Agustos aksami basliyor.
            Upsert(migrationBuilder, 61, "2026-08-21T00:00:00", "2027-05-30T00:00:00",
                "lfp.fr/article/publication-du-calendrier-general-de-la-ligue-1-mcdonald-s-pour-la-saison-2026-2027 (week-end du 23 aout; acilis turu 21.08) | dogrulama 2026-08-31");

            // Türkiye — Süper Lig: TFF 1. hafta 14-17 Agustos 2026, sezon sonu 23 Mayis 2027.
            Upsert(migrationBuilder, 203, "2026-08-14T00:00:00", "2027-05-24T00:00:00",
                "tff.org/default.aspx?pageID=687&ftxtID=50517 (1. hafta 14-17.08.2026) | dogrulama 2026-08-31");

            // Hollanda — Eredivisie: acilis maci Cuma 7 Agustos 2026 (Cambuur-Excelsior),
            // son speelronde 23 Mayis 2027.
            Upsert(migrationBuilder, 88, "2026-08-07T00:00:00", "2027-05-24T00:00:00",
                "eredivisie.nl/nieuws/eerste-speelronde-2026-27/ | dogrulama 2026-08-31");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DELETE FROM [LeagueSeasons] WHERE [SeasonYear] = 2026 AND [LeagueId] IN (39,135,78,61,203,88);");
        }
    }
}
