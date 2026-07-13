using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Radar_R111_OddsMovement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OddsMovementSnapshots",
                columns: table => new
                {
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    PreviousOdds = table.Column<double>(type: "float", nullable: false),
                    CurrentOdds = table.Column<double>(type: "float", nullable: false),
                    Delta = table.Column<double>(type: "float", nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    ComputedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OddsMovementSnapshots", x => x.MatchId);
                });

            migrationBuilder.CreateTable(
                name: "OddsSnapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    SourceId = table.Column<int>(type: "int", nullable: false),
                    HomeOdds = table.Column<double>(type: "float", nullable: false),
                    DrawOdds = table.Column<double>(type: "float", nullable: false),
                    AwayOdds = table.Column<double>(type: "float", nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OddsSnapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OddsSnapshots_MatchId_CapturedAtUtc",
                table: "OddsSnapshots",
                columns: new[] { "MatchId", "CapturedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OddsMovementSnapshots");

            migrationBuilder.DropTable(
                name: "OddsSnapshots");
        }
    }
}
