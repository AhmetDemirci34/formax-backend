using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFeatureStore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MatchFeatureRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HistoricalMatchId = table.Column<int>(type: "int", nullable: false),
                    MatchDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HistoricalCompetitionId = table.Column<int>(type: "int", nullable: false),
                    HomeTeamId = table.Column<int>(type: "int", nullable: false),
                    AwayTeamId = table.Column<int>(type: "int", nullable: false),
                    FeaturesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FeatureHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    LastUpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchFeatureRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchFeatureRecords_HistoricalMatches_HistoricalMatchId",
                        column: x => x.HistoricalMatchId,
                        principalTable: "HistoricalMatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchFeatureRecords_HistoricalMatchId",
                table: "MatchFeatureRecords",
                column: "HistoricalMatchId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchFeatureRecords_MatchDate",
                table: "MatchFeatureRecords",
                column: "MatchDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchFeatureRecords");
        }
    }
}
