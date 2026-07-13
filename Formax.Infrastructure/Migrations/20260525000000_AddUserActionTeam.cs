using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <summary>
    /// Adds the missing UserActions.Team column.
    /// Entity + ModelSnapshot already declared Team, but no migration ever
    /// created the column → "Invalid column name 'Team'" on a DB built purely
    /// from migrations. This migration closes that drift.
    /// </summary>
    [DbContext(typeof(FormaxDbContext))]
    [Migration("20260525000000_AddUserActionTeam")]
    public partial class AddUserActionTeam : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Team",
                table: "UserActions",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Team",
                table: "UserActions");
        }
    }
}
