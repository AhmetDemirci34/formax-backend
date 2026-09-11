using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UserPickSelectionContract : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserPicks_UserId_MatchId",
                table: "UserPicks");

            migrationBuilder.AddColumn<string>(
                name: "MarketGroup",
                table: "UserPicks",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MarketKey",
                table: "UserPicks",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MatchKickoffUtc",
                table: "UserPicks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModelFingerprint",
                table: "UserPicks",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModelVersions",
                table: "UserPicks",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OddAtSelection",
                table: "UserPicks",
                type: "decimal(10,3)",
                precision: 10,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProbabilityPercent",
                table: "UserPicks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelectionStatus",
                table: "UserPicks",
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SettledAtUtc",
                table: "UserPicks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SettlementNote",
                table: "UserPicks",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserPicks_User",
                table: "UserPicks",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UX_UserPicks_User_Match_Market",
                table: "UserPicks",
                columns: new[] { "UserId", "MatchId", "MarketKey" },
                unique: true,
                filter: "[MarketKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserPicks_User",
                table: "UserPicks");

            migrationBuilder.DropIndex(
                name: "UX_UserPicks_User_Match_Market",
                table: "UserPicks");

            migrationBuilder.DropColumn(
                name: "MarketGroup",
                table: "UserPicks");

            migrationBuilder.DropColumn(
                name: "MarketKey",
                table: "UserPicks");

            migrationBuilder.DropColumn(
                name: "MatchKickoffUtc",
                table: "UserPicks");

            migrationBuilder.DropColumn(
                name: "ModelFingerprint",
                table: "UserPicks");

            migrationBuilder.DropColumn(
                name: "ModelVersions",
                table: "UserPicks");

            migrationBuilder.DropColumn(
                name: "OddAtSelection",
                table: "UserPicks");

            migrationBuilder.DropColumn(
                name: "ProbabilityPercent",
                table: "UserPicks");

            migrationBuilder.DropColumn(
                name: "SelectionStatus",
                table: "UserPicks");

            migrationBuilder.DropColumn(
                name: "SettledAtUtc",
                table: "UserPicks");

            migrationBuilder.DropColumn(
                name: "SettlementNote",
                table: "UserPicks");

            migrationBuilder.CreateIndex(
                name: "IX_UserPicks_UserId_MatchId",
                table: "UserPicks",
                columns: new[] { "UserId", "MatchId" },
                unique: true);
        }
    }
}
