using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHistoricalDataPlatform : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HistoricalCompetitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Division = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Country = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    LastUpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricalCompetitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HistoricalTeams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    NormalizedKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Country = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    LastUpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricalTeams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HistoricalEloRatings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HistoricalTeamId = table.Column<int>(type: "int", nullable: true),
                    Club = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Country = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Elo = table.Column<double>(type: "float", nullable: false),
                    SourceKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LastUpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricalEloRatings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistoricalEloRatings_HistoricalTeams_HistoricalTeamId",
                        column: x => x.HistoricalTeamId,
                        principalTable: "HistoricalTeams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "HistoricalMatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HistoricalCompetitionId = table.Column<int>(type: "int", nullable: false),
                    HomeTeamId = table.Column<int>(type: "int", nullable: false),
                    AwayTeamId = table.Column<int>(type: "int", nullable: false),
                    MatchDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MatchTime = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    FTHome = table.Column<int>(type: "int", nullable: true),
                    FTAway = table.Column<int>(type: "int", nullable: true),
                    FTResult = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    HTHome = table.Column<int>(type: "int", nullable: true),
                    HTAway = table.Column<int>(type: "int", nullable: true),
                    HTResult = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    HomeElo = table.Column<double>(type: "float", nullable: true),
                    AwayElo = table.Column<double>(type: "float", nullable: true),
                    Form3Home = table.Column<double>(type: "float", nullable: true),
                    Form5Home = table.Column<double>(type: "float", nullable: true),
                    Form3Away = table.Column<double>(type: "float", nullable: true),
                    Form5Away = table.Column<double>(type: "float", nullable: true),
                    HomeShots = table.Column<int>(type: "int", nullable: true),
                    AwayShots = table.Column<int>(type: "int", nullable: true),
                    HomeTarget = table.Column<int>(type: "int", nullable: true),
                    AwayTarget = table.Column<int>(type: "int", nullable: true),
                    HomeFouls = table.Column<int>(type: "int", nullable: true),
                    AwayFouls = table.Column<int>(type: "int", nullable: true),
                    HomeCorners = table.Column<int>(type: "int", nullable: true),
                    AwayCorners = table.Column<int>(type: "int", nullable: true),
                    HomeYellow = table.Column<int>(type: "int", nullable: true),
                    AwayYellow = table.Column<int>(type: "int", nullable: true),
                    HomeRed = table.Column<int>(type: "int", nullable: true),
                    AwayRed = table.Column<int>(type: "int", nullable: true),
                    OddHome = table.Column<double>(type: "float", nullable: true),
                    OddDraw = table.Column<double>(type: "float", nullable: true),
                    OddAway = table.Column<double>(type: "float", nullable: true),
                    MaxHome = table.Column<double>(type: "float", nullable: true),
                    MaxDraw = table.Column<double>(type: "float", nullable: true),
                    MaxAway = table.Column<double>(type: "float", nullable: true),
                    Over25 = table.Column<double>(type: "float", nullable: true),
                    Under25 = table.Column<double>(type: "float", nullable: true),
                    MaxOver25 = table.Column<double>(type: "float", nullable: true),
                    MaxUnder25 = table.Column<double>(type: "float", nullable: true),
                    HandiSize = table.Column<double>(type: "float", nullable: true),
                    HandiHome = table.Column<double>(type: "float", nullable: true),
                    HandiAway = table.Column<double>(type: "float", nullable: true),
                    SourceKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LastUpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricalMatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistoricalMatches_HistoricalCompetitions_HistoricalCompetitionId",
                        column: x => x.HistoricalCompetitionId,
                        principalTable: "HistoricalCompetitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistoricalMatches_HistoricalTeams_AwayTeamId",
                        column: x => x.AwayTeamId,
                        principalTable: "HistoricalTeams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistoricalMatches_HistoricalTeams_HomeTeamId",
                        column: x => x.HomeTeamId,
                        principalTable: "HistoricalTeams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalCompetitions_Division",
                table: "HistoricalCompetitions",
                column: "Division",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalEloRatings_Club_Date",
                table: "HistoricalEloRatings",
                columns: new[] { "Club", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalEloRatings_HistoricalTeamId",
                table: "HistoricalEloRatings",
                column: "HistoricalTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalEloRatings_SourceKey",
                table: "HistoricalEloRatings",
                column: "SourceKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalMatches_AwayTeamId",
                table: "HistoricalMatches",
                column: "AwayTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalMatches_HistoricalCompetitionId",
                table: "HistoricalMatches",
                column: "HistoricalCompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalMatches_HomeTeamId",
                table: "HistoricalMatches",
                column: "HomeTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalMatches_MatchDate",
                table: "HistoricalMatches",
                column: "MatchDate");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalMatches_SourceKey",
                table: "HistoricalMatches",
                column: "SourceKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalTeams_NormalizedKey",
                table: "HistoricalTeams",
                column: "NormalizedKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistoricalEloRatings");

            migrationBuilder.DropTable(
                name: "HistoricalMatches");

            migrationBuilder.DropTable(
                name: "HistoricalCompetitions");

            migrationBuilder.DropTable(
                name: "HistoricalTeams");
        }
    }
}
