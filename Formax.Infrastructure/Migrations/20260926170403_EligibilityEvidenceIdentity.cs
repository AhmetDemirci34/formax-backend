using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EligibilityEvidenceIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EvidenceFingerprint",
                table: "MarketEligibilityEvaluations",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NewEvidenceCount",
                table: "MarketEligibilityEvaluations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransitionStatus",
                table: "MarketEligibilityEvaluations",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MarketEligibilityEvidenceManifests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    EvaluationCutoffUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModelVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ConfigHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PolicyVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SampleCount = table.Column<int>(type: "int", nullable: false),
                    MaxKickoffUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MaxResultUpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ManifestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Manifest = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reconstructed = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketEligibilityEvidenceManifests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_MarketEligibilityEvidenceManifests_Org_Cutoff_Lineage",
                table: "MarketEligibilityEvidenceManifests",
                columns: new[] { "OrganizationId", "EvaluationCutoffUtc", "ModelVersion", "ConfigHash", "PolicyVersion" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketEligibilityEvidenceManifests");

            migrationBuilder.DropColumn(
                name: "EvidenceFingerprint",
                table: "MarketEligibilityEvaluations");

            migrationBuilder.DropColumn(
                name: "NewEvidenceCount",
                table: "MarketEligibilityEvaluations");

            migrationBuilder.DropColumn(
                name: "TransitionStatus",
                table: "MarketEligibilityEvaluations");
        }
    }
}
