using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PostMatchEventsAndTeamStatistics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MatchEventRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    ExternalFixtureId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ProviderEventId = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Minute = table.Column<int>(type: "int", nullable: false),
                    ExtraMinute = table.Column<int>(type: "int", nullable: true),
                    TeamExternalId = table.Column<int>(type: "int", nullable: true),
                    TeamName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    PlayerName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    AssistName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    EventType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Comments = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FetchedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchEventRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MatchTeamStatistics",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    ExternalFixtureId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Side = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    TeamExternalId = table.Column<int>(type: "int", nullable: true),
                    TeamName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    BallPossession = table.Column<int>(type: "int", nullable: true),
                    TotalShots = table.Column<int>(type: "int", nullable: true),
                    ShotsOnTarget = table.Column<int>(type: "int", nullable: true),
                    ShotsOffTarget = table.Column<int>(type: "int", nullable: true),
                    BlockedShots = table.Column<int>(type: "int", nullable: true),
                    Corners = table.Column<int>(type: "int", nullable: true),
                    Offsides = table.Column<int>(type: "int", nullable: true),
                    Fouls = table.Column<int>(type: "int", nullable: true),
                    YellowCards = table.Column<int>(type: "int", nullable: true),
                    RedCards = table.Column<int>(type: "int", nullable: true),
                    GoalkeeperSaves = table.Column<int>(type: "int", nullable: true),
                    TotalPasses = table.Column<int>(type: "int", nullable: true),
                    PassAccuracy = table.Column<int>(type: "int", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FetchedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchTeamStatistics", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchEventRecords_Match_Minute",
                table: "MatchEventRecords",
                columns: new[] { "MatchId", "Minute" });

            migrationBuilder.CreateIndex(
                name: "UX_MatchEventRecords_Match_Event",
                table: "MatchEventRecords",
                columns: new[] { "MatchId", "ProviderEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_MatchTeamStatistics_Match_Side",
                table: "MatchTeamStatistics",
                columns: new[] { "MatchId", "Side" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchEventRecords");

            migrationBuilder.DropTable(
                name: "MatchTeamStatistics");
        }
    }
}
