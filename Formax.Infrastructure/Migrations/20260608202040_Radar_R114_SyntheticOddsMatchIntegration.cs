using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Radar_R114_SyntheticOddsMatchIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SyntheticDirection",
                table: "MatchIntelligenceSnapshots",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SyntheticSignalLevel",
                table: "MatchIntelligenceSnapshots",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "SyntheticSignalScore",
                table: "MatchIntelligenceSnapshots",
                type: "float",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SyntheticDirection",
                table: "MatchIntelligenceSnapshots");

            migrationBuilder.DropColumn(
                name: "SyntheticSignalLevel",
                table: "MatchIntelligenceSnapshots");

            migrationBuilder.DropColumn(
                name: "SyntheticSignalScore",
                table: "MatchIntelligenceSnapshots");
        }
    }
}
