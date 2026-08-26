using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase6_TeamSeasonStatistics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TeamSeasonStatistics",
                columns: table => new
                {
                    LeagueId = table.Column<int>(type: "int", nullable: false),
                    SeasonYear = table.Column<int>(type: "int", nullable: false),
                    TeamId = table.Column<int>(type: "int", nullable: false),
                    TeamName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PlayedTotal = table.Column<int>(type: "int", nullable: false),
                    PlayedHome = table.Column<int>(type: "int", nullable: false),
                    PlayedAway = table.Column<int>(type: "int", nullable: false),
                    WinsTotal = table.Column<int>(type: "int", nullable: false),
                    DrawsTotal = table.Column<int>(type: "int", nullable: false),
                    LosesTotal = table.Column<int>(type: "int", nullable: false),
                    GoalsForAvgTotal = table.Column<double>(type: "float", nullable: false),
                    GoalsForAvgHome = table.Column<double>(type: "float", nullable: false),
                    GoalsForAvgAway = table.Column<double>(type: "float", nullable: false),
                    GoalsAgainstAvgTotal = table.Column<double>(type: "float", nullable: false),
                    GoalsAgainstAvgHome = table.Column<double>(type: "float", nullable: false),
                    GoalsAgainstAvgAway = table.Column<double>(type: "float", nullable: false),
                    CleanSheetTotal = table.Column<int>(type: "int", nullable: false),
                    FailedToScoreTotal = table.Column<int>(type: "int", nullable: false),
                    Form = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamSeasonStatistics", x => new { x.LeagueId, x.SeasonYear, x.TeamId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeamSeasonStatistics_LeagueId_SeasonYear",
                table: "TeamSeasonStatistics",
                columns: new[] { "LeagueId", "SeasonYear" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeamSeasonStatistics");
        }
    }
}
