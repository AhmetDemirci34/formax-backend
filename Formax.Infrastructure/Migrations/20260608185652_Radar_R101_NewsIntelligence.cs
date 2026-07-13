using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Radar_R101_NewsIntelligence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NewsIntelligenceSnapshots",
                columns: table => new
                {
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    NewsCount = table.Column<int>(type: "int", nullable: false),
                    MentionedTeams = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentionedLeagues = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastNewsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GeneratedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsIntelligenceSnapshots", x => x.MatchId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NewsIntelligenceSnapshots");
        }
    }
}
