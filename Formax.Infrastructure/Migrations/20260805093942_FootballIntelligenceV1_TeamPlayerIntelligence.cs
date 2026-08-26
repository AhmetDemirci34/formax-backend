using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FootballIntelligenceV1_TeamPlayerIntelligence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TeamPlayerIntelligences",
                columns: table => new
                {
                    TeamId = table.Column<int>(type: "int", nullable: false),
                    ExternalTeamId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Season = table.Column<int>(type: "int", nullable: false),
                    HasData = table.Column<bool>(type: "bit", nullable: false),
                    SquadPlayerCount = table.Column<int>(type: "int", nullable: false),
                    GkCount = table.Column<int>(type: "int", nullable: false),
                    DefCount = table.Column<int>(type: "int", nullable: false),
                    MidCount = table.Column<int>(type: "int", nullable: false),
                    AttCount = table.Column<int>(type: "int", nullable: false),
                    TopScorerName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    TopScorerGoals = table.Column<int>(type: "int", nullable: false),
                    TopScorerAssists = table.Column<int>(type: "int", nullable: false),
                    TopScorerRating = table.Column<double>(type: "float", nullable: false),
                    TopAssistName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    TopAssistCount = table.Column<int>(type: "int", nullable: false),
                    KeyPlayerName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    KeyPlayerRating = table.Column<double>(type: "float", nullable: false),
                    MinutesLeaderName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    MinutesLeaderMinutes = table.Column<int>(type: "int", nullable: false),
                    InjuredCount = table.Column<int>(type: "int", nullable: false),
                    InjuredNames = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    InjuredDefCount = table.Column<int>(type: "int", nullable: false),
                    InjuredMidCount = table.Column<int>(type: "int", nullable: false),
                    InjuredAttCount = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamPlayerIntelligences", x => x.TeamId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeamPlayerIntelligences");
        }
    }
}
