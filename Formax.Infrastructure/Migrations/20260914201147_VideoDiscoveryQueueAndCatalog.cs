using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class VideoDiscoveryQueueAndCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MatchVideoDiscoveryAttempts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    ExternalFixtureId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    HomeTeamId = table.Column<int>(type: "int", nullable: false),
                    AwayTeamId = table.Column<int>(type: "int", nullable: false),
                    LeagueId = table.Column<int>(type: "int", nullable: false),
                    Season = table.Column<int>(type: "int", nullable: true),
                    KickoffUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AttemptNo = table.Column<int>(type: "int", nullable: false),
                    AttemptedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowKind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SourceKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    SourceKind = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: true),
                    SourceChannelOrDomain = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SearchExpression = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    HttpStatus = table.Column<int>(type: "int", nullable: true),
                    ErrorType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CandidateUrl = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    CandidateTitle = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CandidatePublishedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DurationSeconds = table.Column<int>(type: "int", nullable: true),
                    VideoType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Accepted = table.Column<bool>(type: "bit", nullable: false),
                    VerificationStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    EmbedResult = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Evidence = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    NextAttemptUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchVideoDiscoveryAttempts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MatchVideoDiscoveryQueue",
                columns: table => new
                {
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    ExternalFixtureId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    HomeTeamId = table.Column<int>(type: "int", nullable: false),
                    AwayTeamId = table.Column<int>(type: "int", nullable: false),
                    LeagueId = table.Column<int>(type: "int", nullable: false),
                    Season = table.Column<int>(type: "int", nullable: true),
                    KickoffUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    State = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    NextAttemptUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastAttemptUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastOutcome = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    LockedUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LockOwner = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    EnqueueReason = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    EnqueuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FoundAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchVideoDiscoveryQueue", x => x.MatchId);
                });

            migrationBuilder.CreateTable(
                name: "OfficialVideoSourceCatalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Publisher = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Platform = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    YouTubeChannelId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    FeedUrl = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Tier = table.Column<int>(type: "int", nullable: false),
                    TeamId = table.Column<int>(type: "int", nullable: true),
                    ClubName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    LeagueIds = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AllowsInAppEmbed = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    DiscoveredVia = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    WikidataId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    OfficialWebsite = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    VerificationEvidence = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VerifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastCheckedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfficialVideoSourceCatalog", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchVideoDiscoveryAttempts_Match",
                table: "MatchVideoDiscoveryAttempts",
                columns: new[] { "MatchId", "AttemptedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchVideoDiscoveryQueue_Due",
                table: "MatchVideoDiscoveryQueue",
                columns: new[] { "NextAttemptUtc", "EndUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OfficialVideoSourceCatalog_Channel",
                table: "OfficialVideoSourceCatalog",
                column: "YouTubeChannelId");

            migrationBuilder.CreateIndex(
                name: "UX_OfficialVideoSourceCatalog_Key",
                table: "OfficialVideoSourceCatalog",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchVideoDiscoveryAttempts");

            migrationBuilder.DropTable(
                name: "MatchVideoDiscoveryQueue");

            migrationBuilder.DropTable(
                name: "OfficialVideoSourceCatalog");
        }
    }
}
