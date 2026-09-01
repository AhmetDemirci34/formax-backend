using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixtureScheduleRefreshLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "KickoffPrecision",
                table: "Matches",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduleRefreshAttemptedAtUtc",
                table: "Matches",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduleVerifiedAtUtc",
                table: "Matches",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FixtureRefreshAttempts",
                columns: table => new
                {
                    ExternalMatchId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DayUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastAttemptUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastOutcome = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FixtureRefreshAttempts", x => new { x.ExternalMatchId, x.Purpose, x.DayUtc });
                });

            migrationBuilder.CreateIndex(
                name: "IX_FixtureRefreshAttempts_Purpose_Day",
                table: "FixtureRefreshAttempts",
                columns: new[] { "Purpose", "DayUtc" });

            // ── MEVCUT KAYITLARIN VARSAYILANI ────────────────────────────────
            // Kolon bos string ile eklendi; hicbir maci "belirsiz" birakmamak icin
            // once TAMAMI Confirmed yapilir, sonra YALNIZ kanitli olanlar Provisional
            // olarak isaretlenir.
            migrationBuilder.Sql(
                "UPDATE [Matches] SET [KickoffPrecision] = N'Confirmed' " +
                "WHERE [KickoffPrecision] IS NULL OR [KickoffPrecision] = N'';");

            // ── KANITA DAYALI BACKFILL ───────────────────────────────────────
            // "Saat 12:00 ise gecicidir" TEK BASINA kanit DEGILDIR: gercek ogle maclari
            // vardir ve boyle bir kural masum maclari damgalar (denendi: 60+ ligde 912
            // yanlis isaretleme). Kullanilan kanit, olculmus DESEN'dir:
            //
            //   Bir ligin BUTUN gelecek fiksturleri TEK BIR saatte topluysa, o saat
            //   gercek bir kickoff degil TUR YER TUTUCUSUDUR. Gercek takvimlerde
            //   kickoff saatleri cesitlidir.
            //
            // OLCUM (01.09.2026, kilitli 11 lig, gelecek NotStarted fiksturler):
            //   203 Super Lig : 161 mac / 1 farkli saat  (12:00)  <- yer tutucu
            //   135 Serie A   : 178 mac / 6 farkli saat
            //   61  Ligue 1   : 162 mac / 6 farkli saat
            //   78  Bundesliga: 120 mac / 7 farkli saat
            //   140 La Liga   : 176 mac / 7 farkli saat
            //   39  Premier L.: 177 mac / 9 farkli saat
            //   40  Championship:202 mac /10 farkli saat
            //   88  Eredivisie: 164 mac /14 farkli saat
            // Tek saatli TEK lig Super Lig'dir; backfill de yalnizca onu isaretler.
            //
            // Ek guvenlik: ligin bu sezon en az 10 BITMIS maci olmali ve o saat o
            // maclarda HIC gorulmemis olmali (lig gercekten o saatte oynamiyor).
            // Ileriye donuk dogru kaynak bu heuristik DEGIL, saglayicinin "TBD" kodudur.
            migrationBuilder.Sql(@"
WITH league_pattern AS (
    SELECT [LeagueId],
           COUNT(*) AS FutureCount,
           COUNT(DISTINCT CAST([MatchDate] AS time)) AS DistinctSlots,
           MIN(CAST([MatchDate] AS time)) AS OnlySlot
      FROM [Matches]
     WHERE [Status] = N'NotStarted' AND [MatchDate] > SYSUTCDATETIME()
       AND [LeagueId] IN (39,40,140,135,78,61,203,88,2,3,848)
     GROUP BY [LeagueId]
    HAVING COUNT(DISTINCT CAST([MatchDate] AS time)) = 1 AND COUNT(*) >= 20
)
UPDATE m
   SET m.[KickoffPrecision] = N'Provisional'
  FROM [Matches] m
  JOIN league_pattern lp
    ON lp.[LeagueId] = m.[LeagueId]
   AND CAST(m.[MatchDate] AS time) = lp.OnlySlot
 WHERE m.[Status] = N'NotStarted'
   AND m.[MatchDate] > SYSUTCDATETIME()
   AND (SELECT COUNT(*) FROM [Matches] f
         WHERE f.[LeagueId] = m.[LeagueId] AND f.[Status] = N'Finished'
           AND f.[MatchDate] >= '2026-07-01') >= 10
   AND NOT EXISTS (
        SELECT 1 FROM [Matches] f
         WHERE f.[LeagueId] = m.[LeagueId] AND f.[Status] = N'Finished'
           AND f.[MatchDate] >= '2026-07-01'
           AND CAST(f.[MatchDate] AS time) = CAST(m.[MatchDate] AS time));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FixtureRefreshAttempts");

            migrationBuilder.DropColumn(
                name: "KickoffPrecision",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "ScheduleRefreshAttemptedAtUtc",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "ScheduleVerifiedAtUtc",
                table: "Matches");
        }
    }
}
