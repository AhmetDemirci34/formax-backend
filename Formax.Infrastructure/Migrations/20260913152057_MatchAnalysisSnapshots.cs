using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MatchAnalysisSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MatchAnalysisSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    KickoffUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GeneratedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InputHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Generator = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ContentJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EvidenceJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FlatText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MaxSimilarity = table.Column<double>(type: "float", nullable: false),
                    MostSimilarMatchId = table.Column<int>(type: "int", nullable: true),
                    RejectedSentenceCount = table.Column<int>(type: "int", nullable: false),
                    RejectionsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LlmCalls = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchAnalysisSnapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchAnalysisSnapshots_Generated",
                table: "MatchAnalysisSnapshots",
                column: "GeneratedAtUtc");

            migrationBuilder.CreateIndex(
                name: "UX_MatchAnalysisSnapshots_Match",
                table: "MatchAnalysisSnapshots",
                column: "MatchId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchAnalysisSnapshots");
        }
    }
}
