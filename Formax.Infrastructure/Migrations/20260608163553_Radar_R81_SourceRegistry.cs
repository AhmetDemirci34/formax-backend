using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Radar_R81_SourceRegistry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SourceDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    FailoverGroup = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Endpoint = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ScheduleExpr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    Lifecycle = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SourceStatuses",
                columns: table => new
                {
                    SourceId = table.Column<int>(type: "int", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    LastSuccessUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastFailureUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataFreshnessAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastNormalizeError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SuccessCount = table.Column<long>(type: "bigint", nullable: false),
                    ErrorCount = table.Column<long>(type: "bigint", nullable: false),
                    TimeoutCount = table.Column<long>(type: "bigint", nullable: false),
                    ConsecutiveFailures = table.Column<int>(type: "int", nullable: false),
                    SuccessRate = table.Column<double>(type: "float", nullable: false),
                    DuplicateRate = table.Column<double>(type: "float", nullable: false),
                    AvgDurationMs = table.Column<double>(type: "float", nullable: false),
                    HealthScore = table.Column<double>(type: "float", nullable: false),
                    LastVolume = table.Column<int>(type: "int", nullable: false),
                    ExpectedVolume = table.Column<int>(type: "int", nullable: false),
                    CircuitState = table.Column<int>(type: "int", nullable: false),
                    CircuitOpenedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceStatuses", x => x.SourceId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SourceDefinitions_SourceKey",
                table: "SourceDefinitions",
                column: "SourceKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SourceDefinitions");

            migrationBuilder.DropTable(
                name: "SourceStatuses");
        }
    }
}
