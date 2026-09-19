using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LeagueMarketEligibilityMatrix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LeagueMarketEligibilities",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RunId = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ModelVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PolicyVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    LeagueId = table.Column<int>(type: "int", nullable: false),
                    Family = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    ReasonsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TestMatches = table.Column<int>(type: "int", nullable: false),
                    LogLoss = table.Column<double>(type: "float", nullable: false),
                    BaselineLogLoss = table.Column<double>(type: "float", nullable: false),
                    Brier = table.Column<double>(type: "float", nullable: false),
                    LogLossDiffCiHigh = table.Column<double>(type: "float", nullable: false),
                    CalibrationError = table.Column<double>(type: "float", nullable: false),
                    MaxBias = table.Column<double>(type: "float", nullable: false),
                    MetricsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EvaluatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueMarketEligibilities", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_LeagueMarketEligibilities_Run_League_Family",
                table: "LeagueMarketEligibilities",
                columns: new[] { "RunId", "LeagueId", "Family" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeagueMarketEligibilities");
        }
    }
}
