using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompetitionEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CompetitionId",
                table: "Matches",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Competitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Country = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    LastUpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Competitions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Matches_CompetitionId",
                table: "Matches",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Competitions_Name",
                table: "Competitions",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Matches_Competitions_CompetitionId",
                table: "Matches",
                column: "CompetitionId",
                principalTable: "Competitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Matches_Competitions_CompetitionId",
                table: "Matches");

            migrationBuilder.DropTable(
                name: "Competitions");

            migrationBuilder.DropIndex(
                name: "IX_Matches_CompetitionId",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "CompetitionId",
                table: "Matches");
        }
    }
}
