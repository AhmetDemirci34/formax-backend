using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFeatureStoreTarget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TargetAwayGoals",
                table: "MatchFeatureRecords",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TargetHomeGoals",
                table: "MatchFeatureRecords",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetResult",
                table: "MatchFeatureRecords",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetAwayGoals",
                table: "MatchFeatureRecords");

            migrationBuilder.DropColumn(
                name: "TargetHomeGoals",
                table: "MatchFeatureRecords");

            migrationBuilder.DropColumn(
                name: "TargetResult",
                table: "MatchFeatureRecords");
        }
    }
}
