using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LineupFormationAndGrid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AwayFormation",
                table: "MatchLineups",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HomeFormation",
                table: "MatchLineups",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Grid",
                table: "MatchLineupPlayers",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AwayFormation",
                table: "MatchLineups");

            migrationBuilder.DropColumn(
                name: "HomeFormation",
                table: "MatchLineups");

            migrationBuilder.DropColumn(
                name: "Grid",
                table: "MatchLineupPlayers");
        }
    }
}
