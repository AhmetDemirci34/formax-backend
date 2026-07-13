using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Sprint4_NabizFeedIntelligence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MatchSocialFeedItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: true),
                    TeamId = table.Column<int>(type: "int", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    Author = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AuthorVerified = table.Column<bool>(type: "bit", nullable: false),
                    Headline = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SourceUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentimentScore = table.Column<double>(type: "float", nullable: true),
                    RelevanceScore = table.Column<double>(type: "float", nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchSocialFeedItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchSocialFeedItems_ContentHash",
                table: "MatchSocialFeedItems",
                column: "ContentHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchSocialFeedItems_MatchId_PublishedAt",
                table: "MatchSocialFeedItems",
                columns: new[] { "MatchId", "PublishedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchSocialFeedItems");
        }
    }
}
