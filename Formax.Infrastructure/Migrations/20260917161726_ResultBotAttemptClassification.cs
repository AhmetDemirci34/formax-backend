using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ResultBotAttemptClassification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastErrorClass",
                table: "MatchResultChecks",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastValidationStatus",
                table: "MatchResultChecks",
                type: "nvarchar(48)",
                maxLength: 48,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastErrorClass",
                table: "MatchResultChecks");

            migrationBuilder.DropColumn(
                name: "LastValidationStatus",
                table: "MatchResultChecks");
        }
    }
}
