using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DataEngineV2_MatchNews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MatchNewsArticles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FormaxMatchId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Headline = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    PublishedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Sources = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    SourceCount = table.Column<int>(type: "int", nullable: false),
                    Language = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Clusters = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Confidence = table.Column<int>(type: "int", nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchNewsArticles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchNewsArticles_ContentHash",
                table: "MatchNewsArticles",
                column: "ContentHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchNewsArticles_FormaxMatchId",
                table: "MatchNewsArticles",
                column: "FormaxMatchId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchNewsArticles");
        }
    }
}
