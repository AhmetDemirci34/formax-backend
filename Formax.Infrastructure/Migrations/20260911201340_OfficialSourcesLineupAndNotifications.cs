using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OfficialSourcesLineupAndNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "UserNotifications",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NotificationType",
                table: "UserNotifications",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Route",
                table: "UserNotifications",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DiscoveredAtUtc",
                table: "MatchLineups",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FollowersNotifiedAtUtc",
                table: "MatchLineups",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RawContentHash",
                table: "MatchLineups",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceKey",
                table: "MatchLineups",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SourcePublishedAtUtc",
                table: "MatchLineups",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceUrl",
                table: "MatchLineups",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificationStatus",
                table: "MatchLineups",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VerifiedAtUtc",
                table: "MatchLineups",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OfficialMatchLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    SourceKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    OfficialMatchId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OfficialUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OfficialHomeName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OfficialAwayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OfficialKickoffUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LinkedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VerifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfficialMatchLinks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OfficialSourceCache",
                columns: table => new
                {
                    UrlHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    SourceKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ETag = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LastModified = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FetchedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValidatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfficialSourceCache", x => x.UrlHash);
                });

            migrationBuilder.CreateTable(
                name: "OfficialSourceFetches",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Host = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UrlHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RoundKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    MatchId = table.Column<int>(type: "int", nullable: true),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HttpStatus = table.Column<int>(type: "int", nullable: true),
                    Outcome = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CacheHit = table.Column<bool>(type: "bit", nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ContentChanged = table.Column<bool>(type: "bit", nullable: false),
                    Bytes = table.Column<int>(type: "int", nullable: false),
                    DurationMs = table.Column<int>(type: "int", nullable: false),
                    CandidateCount = table.Column<int>(type: "int", nullable: true),
                    AcceptedCount = table.Column<int>(type: "int", nullable: true),
                    Decision = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfficialSourceFetches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserNotificationPreferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    PrefKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserNotificationPreferences", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_UserNotifications_IdempotencyKey",
                table: "UserNotifications",
                column: "IdempotencyKey",
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_OfficialMatchLinks_Match_Source",
                table: "OfficialMatchLinks",
                columns: new[] { "MatchId", "SourceKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_OfficialMatchLinks_Source_OfficialMatch",
                table: "OfficialMatchLinks",
                columns: new[] { "SourceKey", "OfficialMatchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OfficialSourceFetches_Host_Time",
                table: "OfficialSourceFetches",
                columns: new[] { "Host", "RequestedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OfficialSourceFetches_Match_Purpose",
                table: "OfficialSourceFetches",
                columns: new[] { "MatchId", "Purpose" });

            migrationBuilder.CreateIndex(
                name: "IX_OfficialSourceFetches_Round_Url",
                table: "OfficialSourceFetches",
                columns: new[] { "RoundKey", "UrlHash" });

            migrationBuilder.CreateIndex(
                name: "UX_UserNotificationPreferences_User_Key",
                table: "UserNotificationPreferences",
                columns: new[] { "UserId", "PrefKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OfficialMatchLinks");

            migrationBuilder.DropTable(
                name: "OfficialSourceCache");

            migrationBuilder.DropTable(
                name: "OfficialSourceFetches");

            migrationBuilder.DropTable(
                name: "UserNotificationPreferences");

            migrationBuilder.DropIndex(
                name: "UX_UserNotifications_IdempotencyKey",
                table: "UserNotifications");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "UserNotifications");

            migrationBuilder.DropColumn(
                name: "NotificationType",
                table: "UserNotifications");

            migrationBuilder.DropColumn(
                name: "Route",
                table: "UserNotifications");

            migrationBuilder.DropColumn(
                name: "DiscoveredAtUtc",
                table: "MatchLineups");

            migrationBuilder.DropColumn(
                name: "FollowersNotifiedAtUtc",
                table: "MatchLineups");

            migrationBuilder.DropColumn(
                name: "RawContentHash",
                table: "MatchLineups");

            migrationBuilder.DropColumn(
                name: "SourceKey",
                table: "MatchLineups");

            migrationBuilder.DropColumn(
                name: "SourcePublishedAtUtc",
                table: "MatchLineups");

            migrationBuilder.DropColumn(
                name: "SourceUrl",
                table: "MatchLineups");

            migrationBuilder.DropColumn(
                name: "VerificationStatus",
                table: "MatchLineups");

            migrationBuilder.DropColumn(
                name: "VerifiedAtUtc",
                table: "MatchLineups");
        }
    }
}
