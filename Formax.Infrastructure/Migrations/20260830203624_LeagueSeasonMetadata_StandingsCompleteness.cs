using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LeagueSeasonMetadata_StandingsCompleteness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CompletenessCheckedAtUtc",
                table: "LeagueStandingsSnapshots",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ExpectedCompletedFixtures",
                table: "LeagueStandingsSnapshots",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "IncludedCompletedFixtures",
                table: "LeagueStandingsSnapshots",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsComplete",
                table: "LeagueStandingsSnapshots",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MissingCompletedFixtures",
                table: "LeagueStandingsSnapshots",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "LeagueSeasons",
                columns: table => new
                {
                    LeagueId = table.Column<int>(type: "int", nullable: false),
                    SeasonYear = table.Column<int>(type: "int", nullable: false),
                    StartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    VerifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueSeasons", x => new { x.LeagueId, x.SeasonYear });
                });

            // DOĞRULANMIŞ SEZON METADATA KAYDI.
            // Kimlik LeagueId + SeasonYear'dır; lig ADINA özel kod/sabit YOKTUR. Bu satır
            // La Liga (140) 2026/27 sezonunun doğrulanmış başlangıcıdır (15.08.2026) ve
            // sezon başlangıcının TEK yetkili kaynağıdır. Depodaki ilk fikstür tarihi bu
            // kaydın yerine GEÇMEZ (yalnız teşhis amaçlı taşınır).
            migrationBuilder.InsertData(
                table: "LeagueSeasons",
                columns: new[] { "LeagueId", "SeasonYear", "StartUtc", "EndUtc", "Source", "VerifiedAtUtc", "Notes" },
                values: new object?[]
                {
                    140,
                    2026,
                    new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc),
                    null,
                    "Verified",
                    new DateTime(2026, 8, 30, 0, 0, 0, DateTimeKind.Utc),
                    "Kullanici tarafindan dogrulandi (30.08.2026)"
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeagueSeasons");

            migrationBuilder.DropColumn(
                name: "CompletenessCheckedAtUtc",
                table: "LeagueStandingsSnapshots");

            migrationBuilder.DropColumn(
                name: "ExpectedCompletedFixtures",
                table: "LeagueStandingsSnapshots");

            migrationBuilder.DropColumn(
                name: "IncludedCompletedFixtures",
                table: "LeagueStandingsSnapshots");

            migrationBuilder.DropColumn(
                name: "IsComplete",
                table: "LeagueStandingsSnapshots");

            migrationBuilder.DropColumn(
                name: "MissingCompletedFixtures",
                table: "LeagueStandingsSnapshots");
        }
    }
}
