using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Radar_R87_Monitor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SourceMonitorSnapshots",
                columns: table => new
                {
                    SourceId = table.Column<int>(type: "int", nullable: false),
                    SourceKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AlertType = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    HealthScoreAtEval = table.Column<double>(type: "float", nullable: false),
                    ConsecutiveFailuresAtEval = table.Column<int>(type: "int", nullable: false),
                    LastSuccessAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MinutesSinceLastSuccess = table.Column<double>(type: "float", nullable: false),
                    EvaluatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceMonitorSnapshots", x => x.SourceId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SourceMonitorSnapshots");
        }
    }
}
