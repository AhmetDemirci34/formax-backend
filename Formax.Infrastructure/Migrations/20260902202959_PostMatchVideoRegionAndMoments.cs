using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PostMatchVideoRegionAndMoments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvailableCountries",
                table: "MatchVideos",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EventExtraMinute",
                table: "MatchVideos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EventMinute",
                table: "MatchVideos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EventPlayer",
                table: "MatchVideos",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EventTeam",
                table: "MatchVideos",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRegionRestricted",
                table: "MatchVideos",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvailableCountries",
                table: "MatchVideos");

            migrationBuilder.DropColumn(
                name: "EventExtraMinute",
                table: "MatchVideos");

            migrationBuilder.DropColumn(
                name: "EventMinute",
                table: "MatchVideos");

            migrationBuilder.DropColumn(
                name: "EventPlayer",
                table: "MatchVideos");

            migrationBuilder.DropColumn(
                name: "EventTeam",
                table: "MatchVideos");

            migrationBuilder.DropColumn(
                name: "IsRegionRestricted",
                table: "MatchVideos");
        }
    }
}
