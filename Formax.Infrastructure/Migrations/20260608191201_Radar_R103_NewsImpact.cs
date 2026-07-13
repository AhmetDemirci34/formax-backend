using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Radar_R103_NewsImpact : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ImpactLevel",
                table: "NewsIntelligenceSnapshots",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "ImpactScore",
                table: "NewsIntelligenceSnapshots",
                type: "float",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImpactLevel",
                table: "NewsIntelligenceSnapshots");

            migrationBuilder.DropColumn(
                name: "ImpactScore",
                table: "NewsIntelligenceSnapshots");
        }
    }
}
