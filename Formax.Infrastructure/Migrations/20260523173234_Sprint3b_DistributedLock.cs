using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Sprint3b_DistributedLock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LiveIngestionLocks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    OwnerInstanceId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AcquiredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HeartbeatAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiveIngestionLocks", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LiveIngestionLocks");
        }
    }
}
