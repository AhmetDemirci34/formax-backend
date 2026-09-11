using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MatchLineupSourceIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AwayCoach",
                table: "MatchLineups",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AwayTeamExternalId",
                table: "MatchLineups",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalFixtureId",
                table: "MatchLineups",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HomeCoach",
                table: "MatchLineups",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HomeTeamExternalId",
                table: "MatchLineups",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastCheckedAtUtc",
                table: "MatchLineups",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "MatchLineups",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AwayCoach",
                table: "MatchLineups");

            migrationBuilder.DropColumn(
                name: "AwayTeamExternalId",
                table: "MatchLineups");

            migrationBuilder.DropColumn(
                name: "ExternalFixtureId",
                table: "MatchLineups");

            migrationBuilder.DropColumn(
                name: "HomeCoach",
                table: "MatchLineups");

            migrationBuilder.DropColumn(
                name: "HomeTeamExternalId",
                table: "MatchLineups");

            migrationBuilder.DropColumn(
                name: "LastCheckedAtUtc",
                table: "MatchLineups");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "MatchLineups");
        }
    }
}
