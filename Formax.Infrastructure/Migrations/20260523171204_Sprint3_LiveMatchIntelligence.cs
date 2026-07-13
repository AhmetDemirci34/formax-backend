using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Sprint3_LiveMatchIntelligence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Detail",
                table: "MatchLiveEvents",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MatchLiveStats",
                columns: table => new
                {
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    HomeScore = table.Column<int>(type: "int", nullable: false),
                    AwayScore = table.Column<int>(type: "int", nullable: false),
                    Minute = table.Column<int>(type: "int", nullable: true),
                    Phase = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    PossessionHome = table.Column<int>(type: "int", nullable: false),
                    PossessionAway = table.Column<int>(type: "int", nullable: false),
                    ShotsHome = table.Column<int>(type: "int", nullable: false),
                    ShotsAway = table.Column<int>(type: "int", nullable: false),
                    ShotsOnTargetHome = table.Column<int>(type: "int", nullable: false),
                    ShotsOnTargetAway = table.Column<int>(type: "int", nullable: false),
                    CornersHome = table.Column<int>(type: "int", nullable: false),
                    CornersAway = table.Column<int>(type: "int", nullable: false),
                    FoulsHome = table.Column<int>(type: "int", nullable: false),
                    FoulsAway = table.Column<int>(type: "int", nullable: false),
                    OffsidesHome = table.Column<int>(type: "int", nullable: false),
                    OffsidesAway = table.Column<int>(type: "int", nullable: false),
                    YellowHome = table.Column<int>(type: "int", nullable: false),
                    YellowAway = table.Column<int>(type: "int", nullable: false),
                    RedHome = table.Column<int>(type: "int", nullable: false),
                    RedAway = table.Column<int>(type: "int", nullable: false),
                    DangerousAttacksHome = table.Column<int>(type: "int", nullable: false),
                    DangerousAttacksAway = table.Column<int>(type: "int", nullable: false),
                    XgHome = table.Column<double>(type: "float", nullable: true),
                    XgAway = table.Column<double>(type: "float", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchLiveStats", x => x.MatchId);
                });

            migrationBuilder.CreateTable(
                name: "MatchMomentumSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    MinuteBucket = table.Column<int>(type: "int", nullable: false),
                    HomePressure = table.Column<int>(type: "int", nullable: false),
                    AwayPressure = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchMomentumSnapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchMomentumSnapshots_MatchId_MinuteBucket",
                table: "MatchMomentumSnapshots",
                columns: new[] { "MatchId", "MinuteBucket" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchLiveStats");

            migrationBuilder.DropTable(
                name: "MatchMomentumSnapshots");

            migrationBuilder.DropColumn(
                name: "Detail",
                table: "MatchLiveEvents");
        }
    }
}
