using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Radar_R104_NewsMatchIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NewsImpactLevel",
                table: "MatchIntelligenceSnapshots",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "NewsImpactScore",
                table: "MatchIntelligenceSnapshots",
                type: "float",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NewsImpactLevel",
                table: "MatchIntelligenceSnapshots");

            migrationBuilder.DropColumn(
                name: "NewsImpactScore",
                table: "MatchIntelligenceSnapshots");
        }
    }
}
