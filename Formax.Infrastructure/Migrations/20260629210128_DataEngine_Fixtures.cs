using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DataEngine_Fixtures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Fixtures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FormaxMatchId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Country = table.Column<string>(type: "nvarchar(96)", maxLength: 96, nullable: false),
                    League = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Season = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Round = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    KickoffUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HomeTeam = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    AwayTeam = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Venue = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Confidence = table.Column<int>(type: "int", nullable: false),
                    Sources = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fixtures", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Fixtures_FormaxMatchId",
                table: "Fixtures",
                column: "FormaxMatchId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Fixtures_KickoffUtc",
                table: "Fixtures",
                column: "KickoffUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Fixtures");
        }
    }
}
