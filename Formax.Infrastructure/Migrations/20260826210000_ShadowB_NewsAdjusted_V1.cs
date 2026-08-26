using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <summary>
    /// SHADOW B — NEWS_ADJUSTED deney hattının şeması.
    ///
    /// SHADOW A'YA DOKUNMAZ. Bu migration YALNIZ üç yeni tablo ekler; <c>Predictions</c> ve
    /// <c>PredictionSettlements</c> tablolarının sütunları, kısıtları ve tetikleyicileri
    /// olduğu gibi kalır — tek bir ALTER bile yoktur.
    ///
    /// NEDEN ELLE YAZILDI: depoda önceki fazlardan gelen, migration'a dönüşmemiş geniş bir
    /// model kayması var. `dotnet ef migrations add` çalıştırılsaydı o kaymanın tamamını da
    /// şemaya basardı — yani gözden geçirilmemiş bir sürü değişiklik, Shadow B'nin arkasına
    /// gizlenmiş olarak üretim veritabanına giderdi. Bu migration bilerek yalnız kendi
    /// tablolarını oluşturur ve model snapshot'ına DOKUNMAZ.
    ///
    /// Her adım IF NOT EXISTS ile korunur: kısmen uygulanmış bir veritabanında tekrar
    /// çalıştırmak güvenlidir.
    ///
    /// KİMLİK ve HEDEF MODEL bu dosyanın Designer eşinde durur
    /// (<c>20260826210000_ShadowB_NewsAdjusted_V1.Designer.cs</c>) — EF'in kendi ürettiği
    /// migration'lardaki düzenin aynısı. Designer, bu migration uygulandıktan SONRAKİ modeli
    /// taşır; ModelSnapshot da aynı modeli gösterir, böylece bir sonraki `migrations add`
    /// boş çıkar.
    /// </summary>
    public partial class ShadowB_NewsAdjusted_V1 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. ShadowBPredictions ────────────────────────────────────────────────
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.ShadowBPredictions','U') IS NULL
BEGIN
CREATE TABLE dbo.ShadowBPredictions (
    Sequence               bigint IDENTITY(1,1) NOT NULL,
    PredictionId           nvarchar(24)  NOT NULL,
    BasePredictionId       nvarchar(24)  NOT NULL,
    Variant                nvarchar(32)  NOT NULL,
    MatchId                int           NOT NULL,
    CanonicalMatchId       nvarchar(32)  NULL,
    FormaxMatchId          nvarchar(32)  NOT NULL,
    MatchDate              datetime2     NOT NULL,
    PredictionTimestamp    datetime2     NOT NULL,
    EvidenceCutoff         datetime2     NULL,
    ModelVersion           nvarchar(64)  NOT NULL,
    TeamStrengthVersion    nvarchar(64)  NOT NULL,
    GateVersion            nvarchar(64)  NOT NULL,
    CalibrationVersion     nvarchar(64)  NOT NULL,
    AdjustmentVersion      nvarchar(64)  NOT NULL,
    BaseHomeProbability    float         NULL,
    BaseDrawProbability    float         NULL,
    BaseAwayProbability    float         NULL,
    HomeProbability        float         NULL,
    DrawProbability        float         NULL,
    AwayProbability        float         NULL,
    PredictionEligible     bit           NOT NULL,
    ConfidenceClass        nvarchar(16)  NOT NULL,
    GateStatus             nvarchar(16)  NOT NULL,
    GateReason             nvarchar(256) NOT NULL,
    EvidenceCount          int           NOT NULL,
    HomeImpact             float         NOT NULL,
    AwayImpact             float         NOT NULL,
    AppliedTilt            float         NOT NULL,
    AdjustmentApplied      bit           NOT NULL,
    AdjustmentReason       nvarchar(1000) NOT NULL,
    ContentHash            nvarchar(32)  NOT NULL,
    ShadowMode             bit           NOT NULL,
    CreatedAt              datetime2     NOT NULL CONSTRAINT DF_ShadowBPredictions_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_ShadowBPredictions PRIMARY KEY (Sequence)
);
END;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ShadowBPredictions_PredictionId')
CREATE UNIQUE INDEX IX_ShadowBPredictions_PredictionId ON dbo.ShadowBPredictions(PredictionId);");
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ShadowBPredictions_Match_Sequence')
CREATE INDEX IX_ShadowBPredictions_Match_Sequence ON dbo.ShadowBPredictions(MatchId, Sequence);");
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ShadowBPredictions_Base')
CREATE INDEX IX_ShadowBPredictions_Base ON dbo.ShadowBPredictions(BasePredictionId);");
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ShadowBPredictions_MatchDate')
CREATE INDEX IX_ShadowBPredictions_MatchDate ON dbo.ShadowBPredictions(MatchDate);");

            // Olasılıklar ya birlikte vardır ve 1'e toplanır, ya da hiç yoktur.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name='CK_ShadowBPredictions_Simplex')
ALTER TABLE dbo.ShadowBPredictions ADD CONSTRAINT CK_ShadowBPredictions_Simplex CHECK (
    (HomeProbability IS NULL AND DrawProbability IS NULL AND AwayProbability IS NULL)
 OR (HomeProbability IS NOT NULL AND DrawProbability IS NOT NULL AND AwayProbability IS NOT NULL
     AND HomeProbability >= 0 AND DrawProbability >= 0 AND AwayProbability >= 0
     AND ABS(HomeProbability + DrawProbability + AwayProbability - 1.0) <= 1e-9)
);");

            // Kanıt kesimi maç saatinden KÜÇÜK olmalı — maç başladıktan sonraki haber giremez.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name='CK_ShadowBPredictions_EvidencePreMatch')
ALTER TABLE dbo.ShadowBPredictions ADD CONSTRAINT CK_ShadowBPredictions_EvidencePreMatch CHECK (
    EvidenceCutoff IS NULL OR EvidenceCutoff < MatchDate
);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name='CK_ShadowBPredictions_Variant')
ALTER TABLE dbo.ShadowBPredictions ADD CONSTRAINT CK_ShadowBPredictions_Variant CHECK (
    Variant = 'NEWS_ADJUSTED'
);");

            // ── 2. ShadowBPredictionEvidence — hangi kanıt bu tahmine girdi ──────────
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.ShadowBPredictionEvidence','U') IS NULL
BEGIN
CREATE TABLE dbo.ShadowBPredictionEvidence (
    Id                   bigint IDENTITY(1,1) NOT NULL,
    PredictionId         nvarchar(24)  NOT NULL,
    EvidenceId           int           NOT NULL,
    EvidenceContentHash  nvarchar(64)  NOT NULL,
    EventType            nvarchar(64)  NOT NULL,
    RelatedTeam          nvarchar(200) NOT NULL,
    Side                 nvarchar(8)   NOT NULL,
    Source               nvarchar(200) NOT NULL,
    SourceQuality        int           NOT NULL,
    Confidence           int           NOT NULL,
    SourceCount          int           NOT NULL,
    PublishedUtc         datetime2     NOT NULL,
    Weight               float         NOT NULL,
    CONSTRAINT PK_ShadowBPredictionEvidence PRIMARY KEY (Id)
);
END;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ShadowBPredictionEvidence_Prediction')
CREATE INDEX IX_ShadowBPredictionEvidence_Prediction ON dbo.ShadowBPredictionEvidence(PredictionId);");
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_ShadowBPredictionEvidence_Prediction_Evidence')
CREATE UNIQUE INDEX UX_ShadowBPredictionEvidence_Prediction_Evidence
    ON dbo.ShadowBPredictionEvidence(PredictionId, EvidenceId);");
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_ShadowBPredictionEvidence_ShadowBPredictions')
ALTER TABLE dbo.ShadowBPredictionEvidence ADD CONSTRAINT FK_ShadowBPredictionEvidence_ShadowBPredictions
    FOREIGN KEY (PredictionId) REFERENCES dbo.ShadowBPredictions(PredictionId);");
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name='CK_ShadowBPredictionEvidence_Side')
ALTER TABLE dbo.ShadowBPredictionEvidence ADD CONSTRAINT CK_ShadowBPredictionEvidence_Side CHECK (
    Side IN ('HOME','AWAY')
);");

            // ── 3. ShadowBPredictionSettlements ──────────────────────────────────────
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.ShadowBPredictionSettlements','U') IS NULL
BEGIN
CREATE TABLE dbo.ShadowBPredictionSettlements (
    PredictionId        nvarchar(24) NOT NULL,
    ActualHomeGoals     int          NOT NULL,
    ActualAwayGoals     int          NOT NULL,
    ActualResult        nvarchar(8)  NOT NULL,
    SettlementTimestamp datetime2    NOT NULL,
    CONSTRAINT PK_ShadowBPredictionSettlements PRIMARY KEY (PredictionId)
);
END;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_ShadowBPredictionSettlements_ShadowBPredictions')
ALTER TABLE dbo.ShadowBPredictionSettlements ADD CONSTRAINT FK_ShadowBPredictionSettlements_ShadowBPredictions
    FOREIGN KEY (PredictionId) REFERENCES dbo.ShadowBPredictions(PredictionId);");

            // Sonuç gollerden türetilir; tutarsız satır şema düzeyinde reddedilir.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name='CK_ShadowBPredictionSettlements_Result')
ALTER TABLE dbo.ShadowBPredictionSettlements ADD CONSTRAINT CK_ShadowBPredictionSettlements_Result CHECK (
    (ActualHomeGoals >  ActualAwayGoals AND ActualResult = 'HomeWin')
 OR (ActualHomeGoals =  ActualAwayGoals AND ActualResult = 'Draw')
 OR (ActualHomeGoals <  ActualAwayGoals AND ActualResult = 'AwayWin')
);");

            // ── 4. DEĞİŞMEZLİK — Shadow A ile aynı koruma ────────────────────────────
            // Yayımlanmış bir B tahmini de düzeltilmez: yeni kanıt = YENİ satır.
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.TR_ShadowBPredictions_NoUpdate','TR') IS NULL
EXEC('CREATE TRIGGER dbo.TR_ShadowBPredictions_NoUpdate ON dbo.ShadowBPredictions
AFTER UPDATE AS
BEGIN
    SET NOCOUNT ON;
    ROLLBACK TRANSACTION;
    RAISERROR(''ShadowBPredictions is INSERT-ONLY: a published Shadow B prediction may not be updated. Insert a new one instead (new PredictionId).'', 16, 1);
END');");

            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.TR_ShadowBPredictions_NoDelete','TR') IS NULL
EXEC('CREATE TRIGGER dbo.TR_ShadowBPredictions_NoDelete ON dbo.ShadowBPredictions
AFTER DELETE AS
BEGIN
    SET NOCOUNT ON;
    ROLLBACK TRANSACTION;
    RAISERROR(''ShadowBPredictions is append-only: a published Shadow B prediction may not be deleted.'', 16, 1);
END');");

            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.TR_ShadowBPredictionEvidence_NoUpdate','TR') IS NULL
EXEC('CREATE TRIGGER dbo.TR_ShadowBPredictionEvidence_NoUpdate ON dbo.ShadowBPredictionEvidence
AFTER UPDATE AS
BEGIN
    SET NOCOUNT ON;
    ROLLBACK TRANSACTION;
    RAISERROR(''ShadowBPredictionEvidence is INSERT-ONLY: the evidence a prediction was built from may not be rewritten.'', 16, 1);
END');");

            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.TR_ShadowBPredictionEvidence_NoDelete','TR') IS NULL
EXEC('CREATE TRIGGER dbo.TR_ShadowBPredictionEvidence_NoDelete ON dbo.ShadowBPredictionEvidence
AFTER DELETE AS
BEGIN
    SET NOCOUNT ON;
    ROLLBACK TRANSACTION;
    RAISERROR(''ShadowBPredictionEvidence is append-only.'', 16, 1);
END');");

            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.TR_ShadowBPredictionSettlements_NoUpdate','TR') IS NULL
EXEC('CREATE TRIGGER dbo.TR_ShadowBPredictionSettlements_NoUpdate ON dbo.ShadowBPredictionSettlements
AFTER UPDATE AS
BEGIN
    SET NOCOUNT ON;
    ROLLBACK TRANSACTION;
    RAISERROR(''ShadowBPredictionSettlements is INSERT-ONLY: a settled result may not be replaced.'', 16, 1);
END');");

            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.TR_ShadowBPredictionSettlements_NoDelete','TR') IS NULL
EXEC('CREATE TRIGGER dbo.TR_ShadowBPredictionSettlements_NoDelete ON dbo.ShadowBPredictionSettlements
AFTER DELETE AS
BEGIN
    SET NOCOUNT ON;
    ROLLBACK TRANSACTION;
    RAISERROR(''ShadowBPredictionSettlements is append-only.'', 16, 1);
END');");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF OBJECT_ID('dbo.TR_ShadowBPredictionSettlements_NoDelete','TR') IS NOT NULL DROP TRIGGER dbo.TR_ShadowBPredictionSettlements_NoDelete;");
            migrationBuilder.Sql("IF OBJECT_ID('dbo.TR_ShadowBPredictionSettlements_NoUpdate','TR') IS NOT NULL DROP TRIGGER dbo.TR_ShadowBPredictionSettlements_NoUpdate;");
            migrationBuilder.Sql("IF OBJECT_ID('dbo.TR_ShadowBPredictionEvidence_NoDelete','TR') IS NOT NULL DROP TRIGGER dbo.TR_ShadowBPredictionEvidence_NoDelete;");
            migrationBuilder.Sql("IF OBJECT_ID('dbo.TR_ShadowBPredictionEvidence_NoUpdate','TR') IS NOT NULL DROP TRIGGER dbo.TR_ShadowBPredictionEvidence_NoUpdate;");
            migrationBuilder.Sql("IF OBJECT_ID('dbo.TR_ShadowBPredictions_NoDelete','TR') IS NOT NULL DROP TRIGGER dbo.TR_ShadowBPredictions_NoDelete;");
            migrationBuilder.Sql("IF OBJECT_ID('dbo.TR_ShadowBPredictions_NoUpdate','TR') IS NOT NULL DROP TRIGGER dbo.TR_ShadowBPredictions_NoUpdate;");

            migrationBuilder.Sql("IF OBJECT_ID('dbo.ShadowBPredictionSettlements','U') IS NOT NULL DROP TABLE dbo.ShadowBPredictionSettlements;");
            migrationBuilder.Sql("IF OBJECT_ID('dbo.ShadowBPredictionEvidence','U') IS NOT NULL DROP TABLE dbo.ShadowBPredictionEvidence;");
            migrationBuilder.Sql("IF OBJECT_ID('dbo.ShadowBPredictions','U') IS NOT NULL DROP TABLE dbo.ShadowBPredictions;");
        }
    }
}
