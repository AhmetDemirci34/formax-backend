using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    public partial class AddLearningSystem : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ❌ TEAM KALDIRILDI (zaten var)

            migrationBuilder.CreateTable(
                name: "GlobalTrends",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    LikeRate = table.Column<double>(type: "float", nullable: false),
                    SkipRate = table.Column<double>(type: "float", nullable: false),
                    Score = table.Column<double>(type: "float", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlobalTrends", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MatchBanditStats",
                columns: table => new
                {
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    Impressions = table.Column<int>(type: "int", nullable: false),
                    Likes = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchBanditStats", x => x.MatchId);
                });

            migrationBuilder.CreateTable(
                name: "UserPreferenceWeights",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    LikeWeight = table.Column<double>(type: "float", nullable: false),
                    SkipWeight = table.Column<double>(type: "float", nullable: false),
                    TeamWeight = table.Column<double>(type: "float", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPreferenceWeights", x => x.UserId);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "GlobalTrends");
            migrationBuilder.DropTable(name: "MatchBanditStats");
            migrationBuilder.DropTable(name: "UserPreferenceWeights");

            // ❌ Team drop kaldırıldı
        }
    }
}