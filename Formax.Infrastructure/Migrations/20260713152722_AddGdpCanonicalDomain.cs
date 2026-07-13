using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGdpCanonicalDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CompetitionId",
                table: "Matches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VenueId",
                table: "Matches",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Coaches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Nationality = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastUpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Coaches", x => x.Id);
                });

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

            migrationBuilder.CreateTable(
                name: "CompetitionStandings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompetitionName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    TeamName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Played = table.Column<int>(type: "int", nullable: false),
                    Won = table.Column<int>(type: "int", nullable: false),
                    Drawn = table.Column<int>(type: "int", nullable: false),
                    Lost = table.Column<int>(type: "int", nullable: false),
                    GoalsFor = table.Column<int>(type: "int", nullable: false),
                    GoalsAgainst = table.Column<int>(type: "int", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: false),
                    LastUpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionStandings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GdpMatchLinks",
                columns: table => new
                {
                    FormaxMatchId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    Round = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LastUpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GdpMatchLinks", x => x.FormaxMatchId);
                });

            migrationBuilder.CreateTable(
                name: "HeadToHeads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FormaxMatchId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    HomeTeam = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AwayTeam = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastUpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HeadToHeads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Injuries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlayerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TeamName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastUpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Injuries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Lineups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FormaxMatchId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TeamName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Formation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Players = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastUpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lineups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MatchStatistics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FormaxMatchId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TeamName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Metrics = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastUpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchStatistics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MatchWeathers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FormaxMatchId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    Longitude = table.Column<double>(type: "float", nullable: true),
                    TemperatureC = table.Column<double>(type: "float", nullable: true),
                    Condition = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ForecastUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastUpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchWeathers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NewsArticles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FormaxMatchId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Url = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PublishedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastUpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsArticles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Position = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Nationality = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastUpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Referees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Nationality = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastUpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Referees", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Suspensions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlayerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TeamName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Matches = table.Column<int>(type: "int", nullable: true),
                    LastUpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suspensions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Transfers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlayerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FromTeam = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ToTeam = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Fee = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TransferDateUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastUpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transfers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Venues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    City = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Country = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Capacity = table.Column<int>(type: "int", nullable: true),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    Longitude = table.Column<double>(type: "float", nullable: true),
                    LastUpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Venues", x => x.Id);
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
                name: "IX_Matches_CompetitionId",
                table: "Matches",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_VenueId",
                table: "Matches",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_Competitions_Name",
                table: "Competitions",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionStandings_CompetitionName_TeamName",
                table: "CompetitionStandings",
                columns: new[] { "CompetitionName", "TeamName" },
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_HeadToHeads_FormaxMatchId",
                table: "HeadToHeads",
                column: "FormaxMatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Lineups_FormaxMatchId",
                table: "Lineups",
                column: "FormaxMatchId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchStatistics_FormaxMatchId",
                table: "MatchStatistics",
                column: "FormaxMatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Venues_Name",
                table: "Venues",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Matches_Competitions_CompetitionId",
                table: "Matches",
                column: "CompetitionId",
                principalTable: "Competitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Matches_Venues_VenueId",
                table: "Matches",
                column: "VenueId",
                principalTable: "Venues",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Matches_Competitions_CompetitionId",
                table: "Matches");

            migrationBuilder.DropForeignKey(
                name: "FK_Matches_Venues_VenueId",
                table: "Matches");

            migrationBuilder.DropTable(
                name: "Coaches");

            migrationBuilder.DropTable(
                name: "Competitions");

            migrationBuilder.DropTable(
                name: "CompetitionStandings");

            migrationBuilder.DropTable(
                name: "GdpConflictResolutions");

            migrationBuilder.DropTable(
                name: "GdpProviderFieldProvenances");

            migrationBuilder.DropTable(
                name: "GdpProviderMatchReferences");

            migrationBuilder.DropTable(
                name: "HeadToHeads");

            migrationBuilder.DropTable(
                name: "Injuries");

            migrationBuilder.DropTable(
                name: "Lineups");

            migrationBuilder.DropTable(
                name: "MatchStatistics");

            migrationBuilder.DropTable(
                name: "MatchWeathers");

            migrationBuilder.DropTable(
                name: "NewsArticles");

            migrationBuilder.DropTable(
                name: "Players");

            migrationBuilder.DropTable(
                name: "Referees");

            migrationBuilder.DropTable(
                name: "Suspensions");

            migrationBuilder.DropTable(
                name: "Transfers");

            migrationBuilder.DropTable(
                name: "Venues");

            migrationBuilder.DropTable(
                name: "GdpMatchLinks");

            migrationBuilder.DropIndex(
                name: "IX_Matches_CompetitionId",
                table: "Matches");

            migrationBuilder.DropIndex(
                name: "IX_Matches_VenueId",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "CompetitionId",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "VenueId",
                table: "Matches");
        }
    }
}
