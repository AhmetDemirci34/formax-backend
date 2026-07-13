using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GdpMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NOT: UserActions.Team kolonu ve GlobalTrends tablosu, bu migration'ın
            // eski/kesik snapshot üzerinde üretilmesi nedeniyle yanlışlıkla buraya süpürülmüştü;
            // ikisi de gerçek FormaxDB'de zaten mevcut (Team → 20260525000000_AddUserActionTeam).
            // Lineage ayrışması düzeltmesi olarak bu iki çoğaltma operasyonu kaldırıldı.
            migrationBuilder.CreateTable(
                name: "GdpMatchLinks",
                columns: table => new
                {
                    FormaxMatchId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    LastUpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GdpMatchLinks", x => x.FormaxMatchId);
                });

            migrationBuilder.CreateTable(
                name: "GdpConflictResolutions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FormaxMatchId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Field = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SelectedProvider = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResolutionStrategy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Confidence = table.Column<double>(type: "float", nullable: false),
                    IsResolved = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GdpConflictResolutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GdpConflictResolutions_GdpMatchLinks_FormaxMatchId",
                        column: x => x.FormaxMatchId,
                        principalTable: "GdpMatchLinks",
                        principalColumn: "FormaxMatchId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GdpProviderFieldProvenances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FormaxMatchId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Field = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    State = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GdpProviderFieldProvenances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GdpProviderFieldProvenances_GdpMatchLinks_FormaxMatchId",
                        column: x => x.FormaxMatchId,
                        principalTable: "GdpMatchLinks",
                        principalColumn: "FormaxMatchId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GdpProviderMatchReferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FormaxMatchId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ProviderName = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderMatchId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GdpProviderMatchReferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GdpProviderMatchReferences_GdpMatchLinks_FormaxMatchId",
                        column: x => x.FormaxMatchId,
                        principalTable: "GdpMatchLinks",
                        principalColumn: "FormaxMatchId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GdpConflictResolutions_FormaxMatchId",
                table: "GdpConflictResolutions",
                column: "FormaxMatchId");

            migrationBuilder.CreateIndex(
                name: "IX_GdpMatchLinks_MatchId",
                table: "GdpMatchLinks",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_GdpProviderFieldProvenances_FormaxMatchId",
                table: "GdpProviderFieldProvenances",
                column: "FormaxMatchId");

            migrationBuilder.CreateIndex(
                name: "IX_GdpProviderMatchReferences_FormaxMatchId_ProviderName_ProviderMatchId",
                table: "GdpProviderMatchReferences",
                columns: new[] { "FormaxMatchId", "ProviderName", "ProviderMatchId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GdpConflictResolutions");

            migrationBuilder.DropTable(
                name: "GdpProviderFieldProvenances");

            migrationBuilder.DropTable(
                name: "GdpProviderMatchReferences");

            migrationBuilder.DropTable(
                name: "GdpMatchLinks");
        }
    }
}
