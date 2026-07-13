using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Radar_R86_Health : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SourceHealthSnapshots",
                columns: table => new
                {
                    SourceId = table.Column<int>(type: "int", nullable: false),
                    SourceKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TotalExecutions = table.Column<long>(type: "bigint", nullable: false),
                    SuccessCount = table.Column<long>(type: "bigint", nullable: false),
                    FailureCount = table.Column<long>(type: "bigint", nullable: false),
                    ConsecutiveFailures = table.Column<int>(type: "int", nullable: false),
                    LastSuccessAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastFailureAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SuccessRate = table.Column<double>(type: "float", nullable: false),
                    FailureRate = table.Column<double>(type: "float", nullable: false),
                    AvgExecutionTimeMs = table.Column<double>(type: "float", nullable: false),
                    HealthScore = table.Column<double>(type: "float", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceHealthSnapshots", x => x.SourceId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SourceHealthSnapshots");
        }
    }
}
