using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PostMatchContentAndVideos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MatchPostContentLinks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Confidence = table.Column<int>(type: "int", nullable: false),
                    MatchReason = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VerifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchPostContentLinks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MatchVideos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    ExternalVideoId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    SourceName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    SourceUrl = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    EmbedUrl = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    ThumbnailUrl = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    VideoType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsOfficial = table.Column<bool>(type: "bit", nullable: false),
                    IsEmbeddable = table.Column<bool>(type: "bit", nullable: false),
                    VerifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchVideos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_MatchPostContentLinks_Match_Content",
                table: "MatchPostContentLinks",
                columns: new[] { "MatchId", "ContentHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_MatchVideos_Match_Video",
                table: "MatchVideos",
                columns: new[] { "MatchId", "ExternalVideoId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchPostContentLinks");

            migrationBuilder.DropTable(
                name: "MatchVideos");
        }
    }
}
