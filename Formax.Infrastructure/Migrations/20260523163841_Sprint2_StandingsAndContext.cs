using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Sprint2_StandingsAndContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompetitionContexts",
                columns: table => new
                {
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    CompetitionType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StageName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ContextHeadline = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContextSummary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    BracketJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionContexts", x => x.MatchId);
                });

            migrationBuilder.CreateTable(
                name: "LeagueExternalMappings",
                columns: table => new
                {
                    LeagueId = table.Column<int>(type: "int", nullable: false),
                    ExternalLeagueId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SeasonYear = table.Column<int>(type: "int", nullable: false),
                    LeagueName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueExternalMappings", x => x.LeagueId);
                });

            migrationBuilder.CreateTable(
                name: "LeagueStandings",
                columns: table => new
                {
                    LeagueId = table.Column<int>(type: "int", nullable: false),
                    SeasonYear = table.Column<int>(type: "int", nullable: false),
                    TeamId = table.Column<int>(type: "int", nullable: false),
                    TeamName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    Played = table.Column<int>(type: "int", nullable: false),
                    Won = table.Column<int>(type: "int", nullable: false),
                    Drawn = table.Column<int>(type: "int", nullable: false),
                    Lost = table.Column<int>(type: "int", nullable: false),
                    GoalsFor = table.Column<int>(type: "int", nullable: false),
                    GoalsAgainst = table.Column<int>(type: "int", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false),
                    Form = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueStandings", x => new { x.LeagueId, x.SeasonYear, x.TeamId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeagueStandings_LeagueId_SeasonYear",
                table: "LeagueStandings",
                columns: new[] { "LeagueId", "SeasonYear" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompetitionContexts");

            migrationBuilder.DropTable(
                name: "LeagueExternalMappings");

            migrationBuilder.DropTable(
                name: "LeagueStandings");
        }
    }
}
