using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase6_TeamProfileSignals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TeamProfileSignals",
                columns: table => new
                {
                    TeamId = table.Column<int>(type: "int", nullable: false),
                    ExternalTeamId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    HasCoach = table.Column<bool>(type: "bit", nullable: false),
                    CoachName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    CoachAge = table.Column<int>(type: "int", nullable: false),
                    HasVenue = table.Column<bool>(type: "bit", nullable: false),
                    VenueName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    VenueCity = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    VenueCapacity = table.Column<int>(type: "int", nullable: false),
                    VenueSurface = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    HasSquad = table.Column<bool>(type: "bit", nullable: false),
                    SquadSize = table.Column<int>(type: "int", nullable: false),
                    SquadAvgAge = table.Column<double>(type: "float", nullable: false),
                    HasTransfers = table.Column<bool>(type: "bit", nullable: false),
                    RecentTransfersIn = table.Column<int>(type: "int", nullable: false),
                    RecentTransfersOut = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamProfileSignals", x => x.TeamId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeamProfileSignals");
        }
    }
}
