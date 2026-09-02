using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PostMatchVideoIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SourceUrl",
                table: "MatchVideos",
                newName: "SourcePageUrl");

            migrationBuilder.RenameColumn(
                name: "SourceName",
                table: "MatchVideos",
                newName: "OfficialPublisher");

            migrationBuilder.AddColumn<int>(
                name: "AwayTeamId",
                table: "MatchVideos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "CanPlayInApp",
                table: "MatchVideos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "DurationSeconds",
                table: "MatchVideos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalFixtureId",
                table: "MatchVideos",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "HomeTeamId",
                table: "MatchVideos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "MatchDateUtc",
                table: "MatchVideos",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "VerificationNote",
                table: "MatchVideos",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "VerificationStatus",
                table: "MatchVideos",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_MatchVideos_Match_Playable",
                table: "MatchVideos",
                columns: new[] { "MatchId", "CanPlayInApp" });

            migrationBuilder.CreateIndex(
                name: "UX_MatchVideos_Match_SourcePage",
                table: "MatchVideos",
                columns: new[] { "MatchId", "SourcePageUrl" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MatchVideos_Match_Playable",
                table: "MatchVideos");

            migrationBuilder.DropIndex(
                name: "UX_MatchVideos_Match_SourcePage",
                table: "MatchVideos");

            migrationBuilder.DropColumn(
                name: "AwayTeamId",
                table: "MatchVideos");

            migrationBuilder.DropColumn(
                name: "CanPlayInApp",
                table: "MatchVideos");

            migrationBuilder.DropColumn(
                name: "DurationSeconds",
                table: "MatchVideos");

            migrationBuilder.DropColumn(
                name: "ExternalFixtureId",
                table: "MatchVideos");

            migrationBuilder.DropColumn(
                name: "HomeTeamId",
                table: "MatchVideos");

            migrationBuilder.DropColumn(
                name: "MatchDateUtc",
                table: "MatchVideos");

            migrationBuilder.DropColumn(
                name: "VerificationNote",
                table: "MatchVideos");

            migrationBuilder.DropColumn(
                name: "VerificationStatus",
                table: "MatchVideos");

            migrationBuilder.RenameColumn(
                name: "SourcePageUrl",
                table: "MatchVideos",
                newName: "SourceUrl");

            migrationBuilder.RenameColumn(
                name: "OfficialPublisher",
                table: "MatchVideos",
                newName: "SourceName");
        }
    }
}
