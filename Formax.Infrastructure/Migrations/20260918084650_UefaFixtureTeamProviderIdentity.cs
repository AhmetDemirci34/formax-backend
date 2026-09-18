using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UefaFixtureTeamProviderIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TeamProviderIdentities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeamId = table.Column<int>(type: "int", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ProviderTeamId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ProviderTeamName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MatchedBy = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    FirstSeenUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VerifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamProviderIdentities", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeamProviderIdentities_Provider_Team",
                table: "TeamProviderIdentities",
                columns: new[] { "Provider", "TeamId" });

            migrationBuilder.CreateIndex(
                name: "UX_TeamProviderIdentities_Provider_ProviderTeam",
                table: "TeamProviderIdentities",
                columns: new[] { "Provider", "ProviderTeamId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeamProviderIdentities");
        }
    }
}
