using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserStatsFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserConfidence_Users_UserId1",
                table: "UserConfidence");

            migrationBuilder.DropIndex(
                name: "IX_UserConfidence_UserId1",
                table: "UserConfidence");

            migrationBuilder.DropColumn(
                name: "UserId1",
                table: "UserConfidence");

            migrationBuilder.AddColumn<int>(
                name: "PassCount",
                table: "UserStats",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PlayCount",
                table: "UserStats",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RiskPreference",
                table: "UserStats",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SafePreference",
                table: "UserStats",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PassCount",
                table: "UserStats");

            migrationBuilder.DropColumn(
                name: "PlayCount",
                table: "UserStats");

            migrationBuilder.DropColumn(
                name: "RiskPreference",
                table: "UserStats");

            migrationBuilder.DropColumn(
                name: "SafePreference",
                table: "UserStats");

            migrationBuilder.AddColumn<int>(
                name: "UserId1",
                table: "UserConfidence",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_UserConfidence_UserId1",
                table: "UserConfidence",
                column: "UserId1");

            migrationBuilder.AddForeignKey(
                name: "FK_UserConfidence_Users_UserId1",
                table: "UserConfidence",
                column: "UserId1",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
