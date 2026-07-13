using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Radar_R112_OddsMovementHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_OddsMovementSnapshots",
                table: "OddsMovementSnapshots");

            migrationBuilder.AddColumn<long>(
                name: "Id",
                table: "OddsMovementSnapshots",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OddsMovementSnapshots",
                table: "OddsMovementSnapshots",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_OddsMovementSnapshots_MatchId_ComputedAtUtc",
                table: "OddsMovementSnapshots",
                columns: new[] { "MatchId", "ComputedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_OddsMovementSnapshots",
                table: "OddsMovementSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_OddsMovementSnapshots_MatchId_ComputedAtUtc",
                table: "OddsMovementSnapshots");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "OddsMovementSnapshots");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OddsMovementSnapshots",
                table: "OddsMovementSnapshots",
                column: "MatchId");
        }
    }
}
