using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLeagueStandingsSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LeagueStandingsSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LeagueId = table.Column<int>(type: "int", nullable: false),
                    SeasonYear = table.Column<int>(type: "int", nullable: false),
                    SeasonStartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CalculatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastIncludedMatchUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    MatchesIncluded = table.Column<int>(type: "int", nullable: false),
                    RankingRuleId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsProvisional = table.Column<bool>(type: "bit", nullable: false),
                    RowsJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueStandingsSnapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeagueStandingsSnapshots_LeagueId_SeasonYear",
                table: "LeagueStandingsSnapshots",
                columns: new[] { "LeagueId", "SeasonYear" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeagueStandingsSnapshots");
        }
    }
}
