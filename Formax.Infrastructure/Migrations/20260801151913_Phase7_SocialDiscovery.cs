using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase7_SocialDiscovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OfficialSocialAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ScopeType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ExternalTeamId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    LeagueId = table.Column<int>(type: "int", nullable: false),
                    Platform = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Handle = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    FeedUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    AccountName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Verified = table.Column<bool>(type: "bit", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfficialSocialAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SocialPosts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FormaxMatchId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Platform = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    AccountHandle = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    AccountName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    RelatedTeamId = table.Column<int>(type: "int", nullable: false),
                    Headline = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    PublishedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SignalType = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                    SourceTrust = table.Column<int>(type: "int", nullable: false),
                    IsOfficial = table.Column<bool>(type: "bit", nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SocialPosts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OfficialSocialAccounts_ExternalTeamId",
                table: "OfficialSocialAccounts",
                column: "ExternalTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_OfficialSocialAccounts_Platform_Handle",
                table: "OfficialSocialAccounts",
                columns: new[] { "Platform", "Handle" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SocialPosts_ContentHash",
                table: "SocialPosts",
                column: "ContentHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SocialPosts_FormaxMatchId",
                table: "SocialPosts",
                column: "FormaxMatchId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OfficialSocialAccounts");

            migrationBuilder.DropTable(
                name: "SocialPosts");
        }
    }
}
