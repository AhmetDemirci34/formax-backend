using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase6_MatchPredictionSignals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MatchPredictionSignals",
                columns: table => new
                {
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    ExternalMatchId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PercentHome = table.Column<int>(type: "int", nullable: false),
                    PercentDraw = table.Column<int>(type: "int", nullable: false),
                    PercentAway = table.Column<int>(type: "int", nullable: false),
                    WinnerName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    WinnerSide = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    WinOrDraw = table.Column<bool>(type: "bit", nullable: false),
                    Advice = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    UnderOver = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ComparisonTotalHome = table.Column<int>(type: "int", nullable: false),
                    ComparisonTotalAway = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchPredictionSignals", x => x.MatchId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchPredictionSignals");
        }
    }
}
