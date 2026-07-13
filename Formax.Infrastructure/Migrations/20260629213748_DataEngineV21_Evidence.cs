using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DataEngineV21_Evidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MatchEvidenceRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FormaxMatchId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                    Cluster = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    SourceQuality = table.Column<int>(type: "int", nullable: false),
                    Confidence = table.Column<int>(type: "int", nullable: false),
                    PublishedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Headline = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchEvidenceRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchEvidenceRecords_ContentHash",
                table: "MatchEvidenceRecords",
                column: "ContentHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchEvidenceRecords_FormaxMatchId",
                table: "MatchEvidenceRecords",
                column: "FormaxMatchId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchEvidenceRecords");
        }
    }
}
