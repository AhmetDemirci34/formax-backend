using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GlobalVideoBackfillWebSourcesAndResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CircuitOpenUntilUtc",
                table: "OfficialVideoSourceCatalog",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CircuitState",
                table: "OfficialVideoSourceCatalog",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: false,
                defaultValue: "Closed");

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "OfficialVideoSourceCatalog",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Domain",
                table: "OfficialVideoSourceCatalog",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FailureCount",
                table: "OfficialVideoSourceCatalog",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "FeedsDiscoveredAtUtc",
                table: "OfficialVideoSourceCatalog",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "OfficialVideoSourceCatalog",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "OfficialVideoSourceCatalog",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSuccessUtc",
                table: "OfficialVideoSourceCatalog",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RobotsStatus",
                table: "OfficialVideoSourceCatalog",
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SiteYouTubeHandles",
                table: "OfficialVideoSourceCatalog",
                type: "nvarchar(600)",
                maxLength: 600,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceKind",
                table: "OfficialVideoSourceCatalog",
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebsiteEvidence",
                table: "OfficialVideoSourceCatalog",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebsiteStatus",
                table: "OfficialVideoSourceCatalog",
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WebsiteVerifiedAtUtc",
                table: "OfficialVideoSourceCatalog",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BlockedAtUtc",
                table: "MatchVideos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscoveryProvenance",
                table: "MatchVideos",
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvidencePageUrl",
                table: "MatchVideos",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvidenceSourceKey",
                table: "MatchVideos",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlayerErrorCode",
                table: "MatchVideos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RevalidatedAtUtc",
                table: "MatchVideos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequeueReason",
                table: "MatchVideoDiscoveryQueue",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RequeuedAtUtc",
                table: "MatchVideoDiscoveryQueue",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MatchResultObservations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    SourceKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    OfficialStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    OfficialHomeScore = table.Column<int>(type: "int", nullable: true),
                    OfficialAwayScore = table.Column<int>(type: "int", nullable: true),
                    ExistingStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    ExistingHomeScore = table.Column<int>(type: "int", nullable: true),
                    ExistingAwayScore = table.Column<int>(type: "int", nullable: true),
                    ExistingSource = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Decision = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ObservedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchResultObservations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OfficialWebFeeds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    DiscoveredVia = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RobotsStatus = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: true),
                    LastFetchedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastHttpStatus = table.Column<int>(type: "int", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    FailureCount = table.Column<int>(type: "int", nullable: false),
                    LastEntryCount = table.Column<int>(type: "int", nullable: false),
                    NextFetchUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfficialWebFeeds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OfficialWebVideoEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    FeedId = table.Column<int>(type: "int", nullable: true),
                    PageUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    PageUrlHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    FoldedTitle = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    PublishedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DatePrecision = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    YouTubeVideoId = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    VideoIdEvidence = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: true),
                    JsonLdName = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    JsonLdUploadUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PageFetchedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PageHttpStatus = table.Column<int>(type: "int", nullable: true),
                    PageOutcome = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: true),
                    FirstSeenUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfficialWebVideoEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VideoDiscoveryCursors",
                columns: table => new
                {
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    LastMatchDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastMatchId = table.Column<int>(type: "int", nullable: true),
                    Pass = table.Column<int>(type: "int", nullable: false),
                    ScannedTotal = table.Column<long>(type: "bigint", nullable: false),
                    EnqueuedTotal = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PassStartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastPassCompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoDiscoveryCursors", x => x.Name);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OfficialVideoSourceCatalog_Domain",
                table: "OfficialVideoSourceCatalog",
                column: "Domain");

            migrationBuilder.CreateIndex(
                name: "IX_MatchResultObservations_Match",
                table: "MatchResultObservations",
                columns: new[] { "MatchId", "ObservedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OfficialWebFeeds_Due",
                table: "OfficialWebFeeds",
                columns: new[] { "IsActive", "NextFetchUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_OfficialWebFeeds_Url",
                table: "OfficialWebFeeds",
                column: "Url",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OfficialWebVideoEntries_SourceDate",
                table: "OfficialWebVideoEntries",
                columns: new[] { "SourceKey", "PublishedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OfficialWebVideoEntries_YouTube",
                table: "OfficialWebVideoEntries",
                column: "YouTubeVideoId");

            migrationBuilder.CreateIndex(
                name: "UX_OfficialWebVideoEntries_Page",
                table: "OfficialWebVideoEntries",
                column: "PageUrlHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchResultObservations");

            migrationBuilder.DropTable(
                name: "OfficialWebFeeds");

            migrationBuilder.DropTable(
                name: "OfficialWebVideoEntries");

            migrationBuilder.DropTable(
                name: "VideoDiscoveryCursors");

            migrationBuilder.DropIndex(
                name: "IX_OfficialVideoSourceCatalog_Domain",
                table: "OfficialVideoSourceCatalog");

            migrationBuilder.DropColumn(
                name: "CircuitOpenUntilUtc",
                table: "OfficialVideoSourceCatalog");

            migrationBuilder.DropColumn(
                name: "CircuitState",
                table: "OfficialVideoSourceCatalog");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "OfficialVideoSourceCatalog");

            migrationBuilder.DropColumn(
                name: "Domain",
                table: "OfficialVideoSourceCatalog");

            migrationBuilder.DropColumn(
                name: "FailureCount",
                table: "OfficialVideoSourceCatalog");

            migrationBuilder.DropColumn(
                name: "FeedsDiscoveredAtUtc",
                table: "OfficialVideoSourceCatalog");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "OfficialVideoSourceCatalog");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "OfficialVideoSourceCatalog");

            migrationBuilder.DropColumn(
                name: "LastSuccessUtc",
                table: "OfficialVideoSourceCatalog");

            migrationBuilder.DropColumn(
                name: "RobotsStatus",
                table: "OfficialVideoSourceCatalog");

            migrationBuilder.DropColumn(
                name: "SiteYouTubeHandles",
                table: "OfficialVideoSourceCatalog");

            migrationBuilder.DropColumn(
                name: "SourceKind",
                table: "OfficialVideoSourceCatalog");

            migrationBuilder.DropColumn(
                name: "WebsiteEvidence",
                table: "OfficialVideoSourceCatalog");

            migrationBuilder.DropColumn(
                name: "WebsiteStatus",
                table: "OfficialVideoSourceCatalog");

            migrationBuilder.DropColumn(
                name: "WebsiteVerifiedAtUtc",
                table: "OfficialVideoSourceCatalog");

            migrationBuilder.DropColumn(
                name: "BlockedAtUtc",
                table: "MatchVideos");

            migrationBuilder.DropColumn(
                name: "DiscoveryProvenance",
                table: "MatchVideos");

            migrationBuilder.DropColumn(
                name: "EvidencePageUrl",
                table: "MatchVideos");

            migrationBuilder.DropColumn(
                name: "EvidenceSourceKey",
                table: "MatchVideos");

            migrationBuilder.DropColumn(
                name: "PlayerErrorCode",
                table: "MatchVideos");

            migrationBuilder.DropColumn(
                name: "RevalidatedAtUtc",
                table: "MatchVideos");

            migrationBuilder.DropColumn(
                name: "RequeueReason",
                table: "MatchVideoDiscoveryQueue");

            migrationBuilder.DropColumn(
                name: "RequeuedAtUtc",
                table: "MatchVideoDiscoveryQueue");
        }
    }
}
