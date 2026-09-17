using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PredictionEligibilityRecomputeScorecard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastNotFinalCheckUtc",
                table: "MatchResultChecks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SourcePublishedFinalAtUtc",
                table: "MatchResultChecks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChangeAuditJson",
                table: "MatchPredictionSnapshots",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EligibilityReasonsJson",
                table: "MatchPredictionSnapshots",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntelligenceFingerprint",
                table: "MatchPredictionSnapshots",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "KickoffUtc",
                table: "MatchPredictionSnapshots",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PredictionEligibility",
                table: "MatchPredictionSnapshots",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousSnapshotId",
                table: "MatchPredictionSnapshots",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublicationStatus",
                table: "MatchPredictionSnapshots",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelectionVersion",
                table: "MatchPredictionSnapshots",
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TriggerSource",
                table: "MatchPredictionSnapshots",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TriggerType",
                table: "MatchPredictionSnapshots",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TriggeredAtUtc",
                table: "MatchPredictionSnapshots",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LeaguePredictionEligibilities",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RunId = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ModelVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PolicyVersion = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    LeagueId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ReasonsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TestMatches = table.Column<int>(type: "int", nullable: false),
                    ResultLogLoss = table.Column<double>(type: "float", nullable: false),
                    BaselineResultLogLoss = table.Column<double>(type: "float", nullable: false),
                    LogLossDiffCiHigh = table.Column<double>(type: "float", nullable: false),
                    CalibrationError = table.Column<double>(type: "float", nullable: false),
                    HomeBias = table.Column<double>(type: "float", nullable: false),
                    DrawBias = table.Column<double>(type: "float", nullable: false),
                    MetricsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EvaluatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaguePredictionEligibilities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PredictionDiagnostics",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    SnapshotId = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    DedupeKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PredictionDiagnostics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PredictionRecomputeRequests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    TriggerType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TriggerSource = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    DedupeKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LockedUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResultSnapshotId = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Outcome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PredictionRecomputeRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PredictionScorecards",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    SnapshotId = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ModelVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    LeagueId = table.Column<int>(type: "int", nullable: false),
                    Eligibility = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    PredictionCreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    KickoffUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LockedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MainCardsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProbabilitiesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FinalHomeScore = table.Column<int>(type: "int", nullable: true),
                    FinalAwayScore = table.Column<int>(type: "int", nullable: true),
                    FinalStatus = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: true),
                    SettledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResultCardCorrect = table.Column<bool>(type: "bit", nullable: true),
                    GoalsCardCorrect = table.Column<bool>(type: "bit", nullable: true),
                    BttsCardCorrect = table.Column<bool>(type: "bit", nullable: true),
                    ResultLogLoss = table.Column<double>(type: "float", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PredictionScorecards", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_LeaguePredictionEligibilities_Run_League",
                table: "LeaguePredictionEligibilities",
                columns: new[] { "RunId", "LeagueId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PredictionDiagnostics_Kind",
                table: "PredictionDiagnostics",
                columns: new[] { "Kind", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_PredictionDiagnostics_DedupeKey",
                table: "PredictionDiagnostics",
                column: "DedupeKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PredictionRecomputeRequests_Due",
                table: "PredictionRecomputeRequests",
                columns: new[] { "Status", "DueAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_PredictionRecomputeRequests_DedupeKey",
                table: "PredictionRecomputeRequests",
                column: "DedupeKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_PredictionScorecards_MatchId",
                table: "PredictionScorecards",
                column: "MatchId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeaguePredictionEligibilities");

            migrationBuilder.DropTable(
                name: "PredictionDiagnostics");

            migrationBuilder.DropTable(
                name: "PredictionRecomputeRequests");

            migrationBuilder.DropTable(
                name: "PredictionScorecards");

            migrationBuilder.DropColumn(
                name: "LastNotFinalCheckUtc",
                table: "MatchResultChecks");

            migrationBuilder.DropColumn(
                name: "SourcePublishedFinalAtUtc",
                table: "MatchResultChecks");

            migrationBuilder.DropColumn(
                name: "ChangeAuditJson",
                table: "MatchPredictionSnapshots");

            migrationBuilder.DropColumn(
                name: "EligibilityReasonsJson",
                table: "MatchPredictionSnapshots");

            migrationBuilder.DropColumn(
                name: "IntelligenceFingerprint",
                table: "MatchPredictionSnapshots");

            migrationBuilder.DropColumn(
                name: "KickoffUtc",
                table: "MatchPredictionSnapshots");

            migrationBuilder.DropColumn(
                name: "PredictionEligibility",
                table: "MatchPredictionSnapshots");

            migrationBuilder.DropColumn(
                name: "PreviousSnapshotId",
                table: "MatchPredictionSnapshots");

            migrationBuilder.DropColumn(
                name: "PublicationStatus",
                table: "MatchPredictionSnapshots");

            migrationBuilder.DropColumn(
                name: "SelectionVersion",
                table: "MatchPredictionSnapshots");

            migrationBuilder.DropColumn(
                name: "TriggerSource",
                table: "MatchPredictionSnapshots");

            migrationBuilder.DropColumn(
                name: "TriggerType",
                table: "MatchPredictionSnapshots");

            migrationBuilder.DropColumn(
                name: "TriggeredAtUtc",
                table: "MatchPredictionSnapshots");
        }
    }
}
