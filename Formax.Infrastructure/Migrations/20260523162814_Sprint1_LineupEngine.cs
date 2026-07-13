using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Sprint1_LineupEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalMatchId",
                table: "Matches",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MatchLineupPlayers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    Side = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ShirtNumber = table.Column<int>(type: "int", nullable: false),
                    PlayerName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Position = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    IsCaptain = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchLineupPlayers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MatchLineups",
                columns: table => new
                {
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    HomeLineupsReleased = table.Column<bool>(type: "bit", nullable: false),
                    AwayLineupsReleased = table.Column<bool>(type: "bit", nullable: false),
                    ReleasedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FetchedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchLineups", x => x.MatchId);
                    table.ForeignKey(
                        name: "FK_MatchLineups_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MatchPlayerStatuses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    TeamId = table.Column<int>(type: "int", nullable: false),
                    PlayerName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FetchedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchPlayerStatuses", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchLineupPlayers_MatchId",
                table: "MatchLineupPlayers",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchPlayerStatuses_MatchId",
                table: "MatchPlayerStatuses",
                column: "MatchId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchLineupPlayers");

            migrationBuilder.DropTable(
                name: "MatchLineups");

            migrationBuilder.DropTable(
                name: "MatchPlayerStatuses");

            migrationBuilder.DropColumn(
                name: "ExternalMatchId",
                table: "Matches");
        }
    }
}
