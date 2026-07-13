using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Radar_R85_Staging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StagedSourceItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceId = table.Column<int>(type: "int", nullable: false),
                    SourceKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    RawId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RawName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CanonicalName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ResolvedTeamId = table.Column<int>(type: "int", nullable: true),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CollectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NormalizedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TtlSeconds = table.Column<int>(type: "int", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Processed = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StagedSourceItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StagedSourceItems_ExpiresAtUtc",
                table: "StagedSourceItems",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_StagedSourceItems_Processed_Category",
                table: "StagedSourceItems",
                columns: new[] { "Processed", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_StagedSourceItems_SourceId",
                table: "StagedSourceItems",
                column: "SourceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StagedSourceItems");
        }
    }
}
