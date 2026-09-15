using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ResultStatisticsBotAndPredictionSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConflictStatus",
                table: "MatchResultObservations",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentHash",
                table: "MatchResultObservations",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OfficialHalfTimeAway",
                table: "MatchResultObservations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OfficialHalfTimeHome",
                table: "MatchResultObservations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OfficialPenaltyAway",
                table: "MatchResultObservations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OfficialPenaltyHome",
                table: "MatchResultObservations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfficialResultDetail",
                table: "MatchResultObservations",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParserVersion",
                table: "MatchResultObservations",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceAwayName",
                table: "MatchResultObservations",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceHomeName",
                table: "MatchResultObservations",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SourceKickoffUtc",
                table: "MatchResultObservations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceMatchId",
                table: "MatchResultObservations",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceUrl",
                table: "MatchResultObservations",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValidationResult",
                table: "MatchResultObservations",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PenaltyAwayScore",
                table: "Matches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PenaltyHomeScore",
                table: "Matches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResultDetail",
                table: "Matches",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MatchPredictionSnapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SnapshotId = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    ModelVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CalibrationRunId = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ComputedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InputsCutoffUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    ExpectedHomeGoals = table.Column<double>(type: "float", nullable: true),
                    ExpectedAwayGoals = table.Column<double>(type: "float", nullable: true),
                    EvidenceCoverage = table.Column<double>(type: "float", nullable: false),
                    HomeSampleSize = table.Column<int>(type: "int", nullable: false),
                    AwaySampleSize = table.Column<int>(type: "int", nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InputHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchPredictionSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MatchResultChecks",
                columns: table => new
                {
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    LeagueId = table.Column<int>(type: "int", nullable: false),
                    KickoffUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    State = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    NextCheckUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastCheckUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastOutcome = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    LastSourceKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    FirstFinalSeenUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedStatus = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    LockedUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LockOwner = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchResultChecks", x => x.MatchId);
                });

            migrationBuilder.CreateTable(
                name: "MatchStatisticObservations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    SourceKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SourceUrl = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Side = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    FieldsJson = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ParserVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ConflictDetail = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ObservedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchStatisticObservations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MatchStatisticsChecks",
                columns: table => new
                {
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    LeagueId = table.Column<int>(type: "int", nullable: false),
                    FinalResultAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    State = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Completeness = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    NextCheckUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastCheckUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastOutcome = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    LastSourceKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    LockedUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LockOwner = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchStatisticsChecks", x => x.MatchId);
                });

            migrationBuilder.CreateTable(
                name: "OfficialDataSources",
                columns: table => new
                {
                    SourceId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SourceName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    OfficialDomain = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OrganizationIds = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ContentKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Capabilities = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    VerificationEvidence = table.Column<string>(type: "nvarchar(1200)", maxLength: 1200, nullable: false),
                    RegistryStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RobotsStatus = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    ParserVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    LastCheckedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSuccessUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ConsecutiveFailureCount = table.Column<int>(type: "int", nullable: false),
                    CircuitBreakerUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfficialDataSources", x => x.SourceId);
                });

            migrationBuilder.CreateTable(
                name: "PredictionModelRuns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RunId = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ModelVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    ParametersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MetricsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TrainMatches = table.Column<int>(type: "int", nullable: false),
                    CalibrationMatches = table.Column<int>(type: "int", nullable: false),
                    TestMatches = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PredictionModelRuns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchPredictionSnapshots_Current",
                table: "MatchPredictionSnapshots",
                columns: new[] { "MatchId", "IsCurrent" });

            migrationBuilder.CreateIndex(
                name: "UX_MatchPredictionSnapshots_SnapshotId",
                table: "MatchPredictionSnapshots",
                column: "SnapshotId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchResultChecks_Due",
                table: "MatchResultChecks",
                columns: new[] { "State", "NextCheckUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchStatisticObservations_Dedupe",
                table: "MatchStatisticObservations",
                columns: new[] { "MatchId", "SourceKey", "Side", "ContentHash" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchStatisticsChecks_Due",
                table: "MatchStatisticsChecks",
                columns: new[] { "State", "NextCheckUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_PredictionModelRuns_RunId",
                table: "PredictionModelRuns",
                column: "RunId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchPredictionSnapshots");

            migrationBuilder.DropTable(
                name: "MatchResultChecks");

            migrationBuilder.DropTable(
                name: "MatchStatisticObservations");

            migrationBuilder.DropTable(
                name: "MatchStatisticsChecks");

            migrationBuilder.DropTable(
                name: "OfficialDataSources");

            migrationBuilder.DropTable(
                name: "PredictionModelRuns");

            migrationBuilder.DropColumn(
                name: "ConflictStatus",
                table: "MatchResultObservations");

            migrationBuilder.DropColumn(
                name: "ContentHash",
                table: "MatchResultObservations");

            migrationBuilder.DropColumn(
                name: "OfficialHalfTimeAway",
                table: "MatchResultObservations");

            migrationBuilder.DropColumn(
                name: "OfficialHalfTimeHome",
                table: "MatchResultObservations");

            migrationBuilder.DropColumn(
                name: "OfficialPenaltyAway",
                table: "MatchResultObservations");

            migrationBuilder.DropColumn(
                name: "OfficialPenaltyHome",
                table: "MatchResultObservations");

            migrationBuilder.DropColumn(
                name: "OfficialResultDetail",
                table: "MatchResultObservations");

            migrationBuilder.DropColumn(
                name: "ParserVersion",
                table: "MatchResultObservations");

            migrationBuilder.DropColumn(
                name: "SourceAwayName",
                table: "MatchResultObservations");

            migrationBuilder.DropColumn(
                name: "SourceHomeName",
                table: "MatchResultObservations");

            migrationBuilder.DropColumn(
                name: "SourceKickoffUtc",
                table: "MatchResultObservations");

            migrationBuilder.DropColumn(
                name: "SourceMatchId",
                table: "MatchResultObservations");

            migrationBuilder.DropColumn(
                name: "SourceUrl",
                table: "MatchResultObservations");

            migrationBuilder.DropColumn(
                name: "ValidationResult",
                table: "MatchResultObservations");

            migrationBuilder.DropColumn(
                name: "PenaltyAwayScore",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "PenaltyHomeScore",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "ResultDetail",
                table: "Matches");
        }
    }
}
