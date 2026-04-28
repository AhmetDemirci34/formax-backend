using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamMediaFields_Fixed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ColorPrimary",
                table: "Teams",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ColorSecondary",
                table: "Teams",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoUrl",
                table: "Teams",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Matches_AwayTeamId",
                table: "Matches",
                column: "AwayTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_HomeTeamId",
                table: "Matches",
                column: "HomeTeamId");

            migrationBuilder.AddForeignKey(
                name: "FK_Matches_Teams_AwayTeamId",
                table: "Matches",
                column: "AwayTeamId",
                principalTable: "Teams",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Matches_Teams_HomeTeamId",
                table: "Matches",
                column: "HomeTeamId",
                principalTable: "Teams",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Matches_Teams_AwayTeamId",
                table: "Matches");

            migrationBuilder.DropForeignKey(
                name: "FK_Matches_Teams_HomeTeamId",
                table: "Matches");

            migrationBuilder.DropIndex(
                name: "IX_Matches_AwayTeamId",
                table: "Matches");

            migrationBuilder.DropIndex(
                name: "IX_Matches_HomeTeamId",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "ColorPrimary",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "ColorSecondary",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "LogoUrl",
                table: "Teams");
        }
    }
}
