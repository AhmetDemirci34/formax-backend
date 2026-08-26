using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFollowFeed_NotificationTargeting_LeagueFollow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "UserNotifications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EventType",
                table: "UserNotifications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LeagueId",
                table: "UserNotifications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoUrl",
                table: "UserNotifications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TargetId",
                table: "UserNotifications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TargetType",
                table: "UserNotifications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TeamId",
                table: "UserNotifications",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserLeagueFollows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    LeagueId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLeagueFollows", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserLeagueFollows");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "UserNotifications");

            migrationBuilder.DropColumn(
                name: "EventType",
                table: "UserNotifications");

            migrationBuilder.DropColumn(
                name: "LeagueId",
                table: "UserNotifications");

            migrationBuilder.DropColumn(
                name: "LogoUrl",
                table: "UserNotifications");

            migrationBuilder.DropColumn(
                name: "TargetId",
                table: "UserNotifications");

            migrationBuilder.DropColumn(
                name: "TargetType",
                table: "UserNotifications");

            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "UserNotifications");
        }
    }
}
