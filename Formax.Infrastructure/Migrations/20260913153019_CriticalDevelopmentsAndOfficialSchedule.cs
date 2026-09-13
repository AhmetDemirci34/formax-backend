using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CriticalDevelopmentsAndOfficialSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OfficialStatus",
                table: "OfficialMatchLinks",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfficialVenue",
                table: "OfficialMatchLinks",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScheduleSource",
                table: "Matches",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MatchCriticalDevelopments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    SourceKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    OfficialUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SourcePublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DevelopmentType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AffectedTeamId = table.Column<int>(type: "int", nullable: true),
                    AffectedPlayerId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Severity = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    VerificationStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    EvidenceHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PreviousValue = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SummaryTr = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    DiscoveredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NotifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NotificationNote = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchCriticalDevelopments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_MatchCriticalDevelopments_Match_Evidence",
                table: "MatchCriticalDevelopments",
                columns: new[] { "MatchId", "EvidenceHash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchCriticalDevelopments");

            migrationBuilder.DropColumn(
                name: "OfficialStatus",
                table: "OfficialMatchLinks");

            migrationBuilder.DropColumn(
                name: "OfficialVenue",
                table: "OfficialMatchLinks");

            migrationBuilder.DropColumn(
                name: "ScheduleSource",
                table: "Matches");
        }
    }
}
