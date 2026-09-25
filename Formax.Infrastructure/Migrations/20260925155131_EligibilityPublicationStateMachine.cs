using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EligibilityPublicationStateMachine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarketEligibilityEvaluations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    MarketFamily = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    EvaluationCutoffUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModelVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ModelRunId = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                    ConfigHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PolicyVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    GatePolicyVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    SampleCount = table.Column<int>(type: "int", nullable: false),
                    LogLoss = table.Column<double>(type: "float", nullable: false),
                    Brier = table.Column<double>(type: "float", nullable: false),
                    Ece = table.Column<double>(type: "float", nullable: false),
                    CalibrationSlope = table.Column<double>(type: "float", nullable: true),
                    CalibrationIntercept = table.Column<double>(type: "float", nullable: true),
                    BaselineLogLoss = table.Column<double>(type: "float", nullable: false),
                    DifferenceFromBaseline = table.Column<double>(type: "float", nullable: false),
                    ConfidenceIntervalLow = table.Column<double>(type: "float", nullable: false),
                    ConfidenceIntervalHigh = table.Column<double>(type: "float", nullable: false),
                    Bias = table.Column<double>(type: "float", nullable: false),
                    Coverage = table.Column<double>(type: "float", nullable: false),
                    GateStatus = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    RawGateStatus = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    RawGateReasonsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EvaluatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PublishedStateBefore = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    PublishedStateAfter = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    TransitionReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PublicationRunKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketEligibilityEvaluations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketEligibilityPublicationRuns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RunKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    EvaluationCutoffUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WeekKey = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ModelVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ConfigHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PolicyVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SourceModelRunId = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    PeakWorkingSetBytes = table.Column<long>(type: "bigint", nullable: false),
                    Cells = table.Column<int>(type: "int", nullable: false),
                    Transitions = table.Column<int>(type: "int", nullable: false),
                    VisibilityChanges = table.Column<int>(type: "int", nullable: false),
                    SummaryJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketEligibilityPublicationRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketEligibilityStates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    MarketFamily = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PublishedState = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    StateVersion = table.Column<int>(type: "int", nullable: false),
                    PolicyVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ModelVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ConfigHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LastEvaluationCutoffUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastRawGateStatus = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: true),
                    LastTransitionReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LastPublicationRunKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketEligibilityStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketEligibilityStateTransitions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    MarketFamily = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FromState = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    ToState = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    StateVersion = table.Column<int>(type: "int", nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PublicationRunKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EvaluationCutoffUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketEligibilityStateTransitions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_MarketEligibilityEvaluations_Cell_Cutoff_Lineage_Mode",
                table: "MarketEligibilityEvaluations",
                columns: new[] { "OrganizationId", "MarketFamily", "EvaluationCutoffUtc", "ModelVersion", "ConfigHash", "PolicyVersion", "Mode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_MarketEligibilityPublicationRuns_RunKey",
                table: "MarketEligibilityPublicationRuns",
                column: "RunKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_MarketEligibilityStates_Cell",
                table: "MarketEligibilityStates",
                columns: new[] { "OrganizationId", "MarketFamily" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketEligibilityStateTransitions_Cell_Version",
                table: "MarketEligibilityStateTransitions",
                columns: new[] { "OrganizationId", "MarketFamily", "StateVersion" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketEligibilityEvaluations");

            migrationBuilder.DropTable(
                name: "MarketEligibilityPublicationRuns");

            migrationBuilder.DropTable(
                name: "MarketEligibilityStates");

            migrationBuilder.DropTable(
                name: "MarketEligibilityStateTransitions");
        }
    }
}
