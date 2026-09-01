using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StandingsNotPlayedFixtures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AbandonedFixtures",
                table: "LeagueStandingsSnapshots",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CancelledFixtures",
                table: "LeagueStandingsSnapshots",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PostponedFixtures",
                table: "LeagueStandingsSnapshots",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StaleResultFixtures",
                table: "LeagueStandingsSnapshots",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AbandonedFixtures",
                table: "LeagueStandingsSnapshots");

            migrationBuilder.DropColumn(
                name: "CancelledFixtures",
                table: "LeagueStandingsSnapshots");

            migrationBuilder.DropColumn(
                name: "PostponedFixtures",
                table: "LeagueStandingsSnapshots");

            migrationBuilder.DropColumn(
                name: "StaleResultFixtures",
                table: "LeagueStandingsSnapshots");
        }
    }
}
