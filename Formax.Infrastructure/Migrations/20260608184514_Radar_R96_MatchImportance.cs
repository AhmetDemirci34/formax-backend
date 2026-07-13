using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Radar_R96_MatchImportance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ImportanceLevel",
                table: "MatchIntelligenceSnapshots",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "ImportanceScore",
                table: "MatchIntelligenceSnapshots",
                type: "float",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImportanceLevel",
                table: "MatchIntelligenceSnapshots");

            migrationBuilder.DropColumn(
                name: "ImportanceScore",
                table: "MatchIntelligenceSnapshots");
        }
    }
}
