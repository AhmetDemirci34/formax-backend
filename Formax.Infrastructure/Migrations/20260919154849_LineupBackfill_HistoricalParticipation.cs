using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LineupBackfill_HistoricalParticipation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "BackfilledAtUtc",
                table: "MatchLineups",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DataQuality",
                table: "MatchLineups",
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinutesPlayed",
                table: "MatchLineupPlayers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfficialPlayerId",
                table: "MatchLineupPlayers",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SubstitutionMinute",
                table: "MatchLineupPlayers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LineupBackfillAttempts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    OfficialMatchId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MatchId = table.Column<int>(type: "int", nullable: true),
                    LeagueId = table.Column<int>(type: "int", nullable: false),
                    SeasonId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    FirstAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LineupBackfillAttempts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LineupBackfillCheckpoints",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    LeagueId = table.Column<int>(type: "int", nullable: false),
                    SeasonId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SeasonLabel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SourceMatches = table.Column<int>(type: "int", nullable: false),
                    MatchedMatches = table.Column<int>(type: "int", nullable: false),
                    ProcessedMatches = table.Column<int>(type: "int", nullable: false),
                    VerifiedMatches = table.Column<int>(type: "int", nullable: false),
                    SkippedMatches = table.Column<int>(type: "int", nullable: false),
                    FailedMatches = table.Column<int>(type: "int", nullable: false),
                    LastOfficialMatchId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastRunAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LineupBackfillCheckpoints", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchLineupPlayers_OfficialPlayerId",
                table: "MatchLineupPlayers",
                column: "OfficialPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_LineupBackfillAttempts_Season_Outcome",
                table: "LineupBackfillAttempts",
                columns: new[] { "SourceKey", "SeasonId", "Outcome" });

            migrationBuilder.CreateIndex(
                name: "UX_LineupBackfillAttempts_Source_Match",
                table: "LineupBackfillAttempts",
                columns: new[] { "SourceKey", "OfficialMatchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_LineupBackfillCheckpoints_Source_Season",
                table: "LineupBackfillCheckpoints",
                columns: new[] { "SourceKey", "SeasonId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LineupBackfillAttempts");

            migrationBuilder.DropTable(
                name: "LineupBackfillCheckpoints");

            migrationBuilder.DropIndex(
                name: "IX_MatchLineupPlayers_OfficialPlayerId",
                table: "MatchLineupPlayers");

            migrationBuilder.DropColumn(
                name: "BackfilledAtUtc",
                table: "MatchLineups");

            migrationBuilder.DropColumn(
                name: "DataQuality",
                table: "MatchLineups");

            migrationBuilder.DropColumn(
                name: "MinutesPlayed",
                table: "MatchLineupPlayers");

            migrationBuilder.DropColumn(
                name: "OfficialPlayerId",
                table: "MatchLineupPlayers");

            migrationBuilder.DropColumn(
                name: "SubstitutionMinute",
                table: "MatchLineupPlayers");
        }
    }
}
