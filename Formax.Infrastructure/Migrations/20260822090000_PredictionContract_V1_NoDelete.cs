using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <summary>
    /// Predictions/PredictionSettlements append-only olmalıydı ama yalnız UPDATE korunuyordu;
    /// DELETE açıktı. Gölge modun doğrulanması sırasında bir test sorgusu gerçekten bir satır
    /// sildi (autocommit'te ROLLBACK işe yaramaz) — açık teorik değil, ölçüldü.
    ///
    /// Kasıtlı bir temizlik gerekirse trigger açıkça DROP edilir; bu, görünür ve niyetli bir
    /// eylem olur. Kaza ile satır kaybı olmaz.
    /// </summary>
    public partial class PredictionContract_V1_NoDelete : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TRIGGER dbo.TR_Predictions_NoDelete ON dbo.Predictions
AFTER DELETE
AS
BEGIN
    SET NOCOUNT ON;
    ROLLBACK TRANSACTION;
    RAISERROR('Predictions is append-only: a published prediction may not be deleted. Drop TR_Predictions_NoDelete explicitly if a purge is genuinely intended.', 16, 1);
END;");

            migrationBuilder.Sql(@"
CREATE TRIGGER dbo.TR_PredictionSettlements_NoDelete ON dbo.PredictionSettlements
AFTER DELETE
AS
BEGIN
    SET NOCOUNT ON;
    ROLLBACK TRANSACTION;
    RAISERROR('PredictionSettlements is append-only: a settled result may not be deleted. Drop TR_PredictionSettlements_NoDelete explicitly if a purge is genuinely intended.', 16, 1);
END;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF OBJECT_ID('dbo.TR_PredictionSettlements_NoDelete','TR') IS NOT NULL DROP TRIGGER dbo.TR_PredictionSettlements_NoDelete;");
            migrationBuilder.Sql("IF OBJECT_ID('dbo.TR_Predictions_NoDelete','TR') IS NOT NULL DROP TRIGGER dbo.TR_Predictions_NoDelete;");
        }
    }
}
