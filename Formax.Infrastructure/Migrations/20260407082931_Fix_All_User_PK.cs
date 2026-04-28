using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Fix_All_User_PK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CouponItems_PredictionTypes_PredictionTypeId",
                table: "CouponItems");

            migrationBuilder.DropTable(
                name: "UserInterestScoreEntity");

            migrationBuilder.DropIndex(
                name: "IX_UserTeamFollows_UserId_TeamId",
                table: "UserTeamFollows");

            migrationBuilder.DropIndex(
                name: "IX_UserInterestEvents_UserId_CreatedAtUtc",
                table: "UserInterestEvents");

            migrationBuilder.DropIndex(
                name: "IX_UserInterestEvents_UserId_EventType",
                table: "UserInterestEvents");

            migrationBuilder.DropIndex(
                name: "IX_MatchSapmaSnapshots_ExpiresAtUtc",
                table: "MatchSapmaSnapshots");

            migrationBuilder.AlterColumn<string>(
                name: "LeagueName",
                table: "UserInterestEvents",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Bolge",
                table: "MatchSapmaSnapshots",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.CreateTable(
                name: "UserConfidence",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId1 = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserConfidence", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserConfidence_Users_UserId1",
                        column: x => x.UserId1,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserPicks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    PickLabel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Confidence = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPicks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserPickStats",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PickLabel = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Total = table.Column<int>(type: "int", nullable: false),
                    Win = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPickStats", x => new { x.UserId, x.PickLabel });
                });

            migrationBuilder.CreateTable(
                name: "UserStats",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Total = table.Column<int>(type: "int", nullable: false),
                    Win = table.Column<int>(type: "int", nullable: false),
                    Lose = table.Column<int>(type: "int", nullable: false),
                    CurrentStreak = table.Column<int>(type: "int", nullable: false),
                    BestStreak = table.Column<int>(type: "int", nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserStats", x => x.UserId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserConfidence_UserId1",
                table: "UserConfidence",
                column: "UserId1");

            migrationBuilder.AddForeignKey(
                name: "FK_CouponItems_PredictionTypes_PredictionTypeId",
                table: "CouponItems",
                column: "PredictionTypeId",
                principalTable: "PredictionTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CouponItems_PredictionTypes_PredictionTypeId",
                table: "CouponItems");

            migrationBuilder.DropTable(
                name: "UserConfidence");

            migrationBuilder.DropTable(
                name: "UserPicks");

            migrationBuilder.DropTable(
                name: "UserPickStats");

            migrationBuilder.DropTable(
                name: "UserStats");

            migrationBuilder.AlterColumn<string>(
                name: "LeagueName",
                table: "UserInterestEvents",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Bolge",
                table: "MatchSapmaSnapshots",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateTable(
                name: "UserInterestScoreEntity",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LastEventAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Layer = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserInterestScoreEntity", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserTeamFollows_UserId_TeamId",
                table: "UserTeamFollows",
                columns: new[] { "UserId", "TeamId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserInterestEvents_UserId_CreatedAtUtc",
                table: "UserInterestEvents",
                columns: new[] { "UserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserInterestEvents_UserId_EventType",
                table: "UserInterestEvents",
                columns: new[] { "UserId", "EventType" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchSapmaSnapshots_ExpiresAtUtc",
                table: "MatchSapmaSnapshots",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_UserInterestScoreEntity_UserId_Layer_Key",
                table: "UserInterestScoreEntity",
                columns: new[] { "UserId", "Layer", "Key" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CouponItems_PredictionTypes_PredictionTypeId",
                table: "CouponItems",
                column: "PredictionTypeId",
                principalTable: "PredictionTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
