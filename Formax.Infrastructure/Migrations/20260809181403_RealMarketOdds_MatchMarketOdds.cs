using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RealMarketOdds_MatchMarketOdds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MatchMarketOdds",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    MarketKey = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Odd = table.Column<decimal>(type: "decimal(8,3)", nullable: false),
                    BookmakerName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    BookmakerId = table.Column<int>(type: "int", nullable: false),
                    PreviousOdd = table.Column<decimal>(type: "decimal(8,3)", nullable: true),
                    CapturedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchMarketOdds", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchMarketOdds_MatchId_MarketKey",
                table: "MatchMarketOdds",
                columns: new[] { "MatchId", "MarketKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchMarketOdds");
        }
    }
}
