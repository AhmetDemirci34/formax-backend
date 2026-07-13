using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Sprint0_FixtureSyncCriticalFixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Teams_ExternalTeamId",
                table: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_Matches_ExternalMatchId",
                table: "Matches");

            migrationBuilder.CreateTable(
                name: "FixtureSyncLocks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    OwnerInstanceId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AcquiredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HeartbeatAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FixtureSyncLocks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Teams_ExternalTeamId",
                table: "Teams",
                column: "ExternalTeamId",
                unique: true,
                filter: "[ExternalTeamId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_ExternalMatchId",
                table: "Matches",
                column: "ExternalMatchId",
                unique: true,
                filter: "[ExternalMatchId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FixtureSyncLocks");

            migrationBuilder.DropIndex(
                name: "IX_Teams_ExternalTeamId",
                table: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_Matches_ExternalMatchId",
                table: "Matches");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_ExternalTeamId",
                table: "Teams",
                column: "ExternalTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_ExternalMatchId",
                table: "Matches",
                column: "ExternalMatchId");
        }
    }
}
