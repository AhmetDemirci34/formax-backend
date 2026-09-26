using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ForwardShadowPredictionLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ForwardPredictionRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    ModelVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ConfigHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SelectorVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SnapshotId = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    LeagueId = table.Column<int>(type: "int", nullable: false),
                    Market = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ProbabilitiesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SelectedOutcome = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    SelectedProbability = table.Column<double>(type: "float", nullable: false),
                    SignalTier = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    MissRisk = table.Column<double>(type: "float", nullable: false),
                    GeneratedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    KickoffUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PredictionLockedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActualOutcome = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: true),
                    ActualHomeGoals = table.Column<int>(type: "int", nullable: true),
                    ActualAwayGoals = table.Column<int>(type: "int", nullable: true),
                    ScoredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LogLoss = table.Column<double>(type: "float", nullable: true),
                    Brier = table.Column<double>(type: "float", nullable: true),
                    Correct = table.Column<bool>(type: "bit", nullable: true),
                    RescoreCount = table.Column<int>(type: "int", nullable: false),
                    ScoreAuditJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForwardPredictionRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ForwardPredictionRecords_ScoredAt",
                table: "ForwardPredictionRecords",
                column: "ScoredAtUtc");

            migrationBuilder.CreateIndex(
                name: "UX_ForwardPredictionRecords_Match_Model_Market",
                table: "ForwardPredictionRecords",
                columns: new[] { "MatchId", "ModelVersion", "Market" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ForwardPredictionRecords");
        }
    }
}
