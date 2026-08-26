using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PredictionContract_V1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Predictions",
                columns: table => new
                {
                    Sequence = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PredictionId = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    CanonicalMatchId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    MatchDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PredictionTimestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EvidenceCutoff = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModelVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TeamStrengthVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    GateVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CalibrationVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    HomeProbability = table.Column<double>(type: "float", nullable: true),
                    DrawProbability = table.Column<double>(type: "float", nullable: true),
                    AwayProbability = table.Column<double>(type: "float", nullable: true),
                    PredictionEligible = table.Column<bool>(type: "bit", nullable: false),
                    ConfidenceClass = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    GateStatus = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    GateReason = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ShadowMode = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Predictions", x => x.Sequence);
                    table.UniqueConstraint("AK_Predictions_PredictionId", x => x.PredictionId);
                });

            migrationBuilder.CreateTable(
                name: "PredictionSettlements",
                columns: table => new
                {
                    PredictionId = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    ActualHomeGoals = table.Column<int>(type: "int", nullable: false),
                    ActualAwayGoals = table.Column<int>(type: "int", nullable: false),
                    ActualResult = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    SettlementTimestamp = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PredictionSettlements", x => x.PredictionId);
                    table.ForeignKey(
                        name: "FK_PredictionSettlements_Predictions_PredictionId",
                        column: x => x.PredictionId,
                        principalTable: "Predictions",
                        principalColumn: "PredictionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_Match_Sequence",
                table: "Predictions",
                columns: new[] { "MatchId", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_MatchDate",
                table: "Predictions",
                column: "MatchDate");

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_PredictionId",
                table: "Predictions",
                column: "PredictionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_Versions",
                table: "Predictions",
                columns: new[] { "ModelVersion", "TeamStrengthVersion", "GateVersion", "CalibrationVersion" });

            // ────────────────────────────────────────────────────────────────
            // Sözleşme kuralları — uygulama katmanına GÜVENMEZ.
            // Uygulama kodu değişebilir; CHECK kısıtını değiştirmek migration ister ve gözden geçirilir.
            // ────────────────────────────────────────────────────────────────

            // Simplex: kabul edilmiş tahminde üç olasılık dolu, [0,1] içinde ve toplamı 1;
            // reddedilmiş tahminde üçü de NULL. GateStatus ile PredictionEligible tutarlı.
            migrationBuilder.Sql(@"
ALTER TABLE dbo.Predictions ADD CONSTRAINT CK_Predictions_Simplex CHECK (
    (PredictionEligible = 0
        AND HomeProbability IS NULL AND DrawProbability IS NULL AND AwayProbability IS NULL
        AND GateStatus = 'REJECTED')
 OR (PredictionEligible = 1
        AND HomeProbability IS NOT NULL AND DrawProbability IS NOT NULL AND AwayProbability IS NOT NULL
        AND HomeProbability >= 0 AND HomeProbability <= 1
        AND DrawProbability >= 0 AND DrawProbability <= 1
        AND AwayProbability >= 0 AND AwayProbability <= 1
        AND ABS(HomeProbability + DrawProbability + AwayProbability - 1.0) <= 1e-9
        AND GateStatus = 'ACCEPTED'
        AND GateReason = 'OK')
);");

            // Kanıt maçtan KESİNLİKLE önceki bir GÜNDE olmalı. Gün karşılaştırması şart:
            // aynı günün 00:00'ı maç saatinden küçüktür ama aynı gün kanıtı sızıntıdır.
            migrationBuilder.Sql(@"
ALTER TABLE dbo.Predictions ADD CONSTRAINT CK_Predictions_EvidencePreMatch CHECK (
    EvidenceCutoff IS NULL OR CAST(EvidenceCutoff AS DATE) < CAST(MatchDate AS DATE)
);");

            migrationBuilder.Sql(@"
ALTER TABLE dbo.Predictions ADD CONSTRAINT CK_Predictions_TimestampAfterEvidence CHECK (
    EvidenceCutoff IS NULL OR CAST(EvidenceCutoff AS DATE) <= CAST(PredictionTimestamp AS DATE)
);");

            // Yayımlanmış bir tahmin ""arkasında hiç kanıt yok"" diyemez.
            migrationBuilder.Sql(@"
ALTER TABLE dbo.Predictions ADD CONSTRAINT CK_Predictions_Confidence CHECK (
    ConfidenceClass IN ('NONE','LOW','MEDIUMLOW','MEDIUM','HIGH')
    AND (PredictionEligible = 0 OR ConfidenceClass <> 'NONE')
);");

            migrationBuilder.Sql(@"
ALTER TABLE dbo.Predictions ADD CONSTRAINT CK_Predictions_GateStatus CHECK (
    GateStatus IN ('ACCEPTED','REJECTED')
);");

            // Sonuç gollerden türetilir; tutarsız kalamaz.
            migrationBuilder.Sql(@"
ALTER TABLE dbo.PredictionSettlements ADD CONSTRAINT CK_PredictionSettlements_Result CHECK (
    ActualHomeGoals >= 0 AND ActualAwayGoals >= 0 AND
    ActualResult = CASE
        WHEN ActualHomeGoals > ActualAwayGoals THEN 'HomeWin'
        WHEN ActualHomeGoals = ActualAwayGoals THEN 'Draw'
        ELSE 'AwayWin' END
);");

            // ────────────────────────────────────────────────────────────────
            // DEĞİŞMEZLİK (§3)
            //
            // DENY UPDATE bir PRINCIPAL'a bağlıdır ve sysadmin'i bağlamaz; FORMAX bağlantısı
            // Trusted_Connection ile yönetici olarak geliyor, dolayısıyla tek başına DENY bu
            // kurulumda hiçbir şeyi engellemezdi. Bu yüzden asıl koruma TRIGGER: kim bağlanırsa
            // bağlansın UPDATE geri alınır. DENY ikinci katman olarak, rol varsa uygulanır.
            // ────────────────────────────────────────────────────────────────
            migrationBuilder.Sql(@"
CREATE TRIGGER dbo.TR_Predictions_NoUpdate ON dbo.Predictions
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    ROLLBACK TRANSACTION;
    RAISERROR('Predictions is INSERT-ONLY: a published prediction may not be updated. Insert a new prediction instead (new PredictionId).', 16, 1);
END;");

            migrationBuilder.Sql(@"
CREATE TRIGGER dbo.TR_PredictionSettlements_NoUpdate ON dbo.PredictionSettlements
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    ROLLBACK TRANSACTION;
    RAISERROR('PredictionSettlements is INSERT-ONLY: a settled result may not be replaced.', 16, 1);
END;");

            // İkinci katman: rol varsa DENY uygula. Rol yoksa sessizce atlanır — migration
            // ortam güvenlik modeline bağımlı olmamalı.
            migrationBuilder.Sql(@"
IF DATABASE_PRINCIPAL_ID('formax_prediction_writer') IS NOT NULL
BEGIN
    DENY UPDATE, DELETE ON dbo.Predictions TO formax_prediction_writer;
    DENY UPDATE, DELETE ON dbo.PredictionSettlements TO formax_prediction_writer;
END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF OBJECT_ID('dbo.TR_PredictionSettlements_NoUpdate','TR') IS NOT NULL DROP TRIGGER dbo.TR_PredictionSettlements_NoUpdate;");
            migrationBuilder.Sql("IF OBJECT_ID('dbo.TR_Predictions_NoUpdate','TR') IS NOT NULL DROP TRIGGER dbo.TR_Predictions_NoUpdate;");

            migrationBuilder.DropTable(
                name: "PredictionSettlements");

            migrationBuilder.DropTable(
                name: "Predictions");
        }
    }
}
