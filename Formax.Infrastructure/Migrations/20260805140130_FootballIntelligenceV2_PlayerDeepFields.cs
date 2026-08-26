using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FootballIntelligenceV2_PlayerDeepFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CardRiskName",
                table: "TeamPlayerIntelligences",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "CardRiskYellows",
                table: "TeamPlayerIntelligences",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DefenseLeaderName",
                table: "TeamPlayerIntelligences",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<double>(
                name: "DefenseLeaderRating",
                table: "TeamPlayerIntelligences",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "KeyPassLeaderCount",
                table: "TeamPlayerIntelligences",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "KeyPassLeaderName",
                table: "TeamPlayerIntelligences",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "MidfieldBrainAssists",
                table: "TeamPlayerIntelligences",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "MidfieldBrainName",
                table: "TeamPlayerIntelligences",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<double>(
                name: "MidfieldBrainRating",
                table: "TeamPlayerIntelligences",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<bool>(
                name: "OneManDependency",
                table: "TeamPlayerIntelligences",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ShotsLeaderCount",
                table: "TeamPlayerIntelligences",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ShotsLeaderName",
                table: "TeamPlayerIntelligences",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "TeamTotalGoals",
                table: "TeamPlayerIntelligences",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Top2GoalSharePct",
                table: "TeamPlayerIntelligences",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TopScorerGoalSharePct",
                table: "TeamPlayerIntelligences",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CardRiskName",
                table: "TeamPlayerIntelligences");

            migrationBuilder.DropColumn(
                name: "CardRiskYellows",
                table: "TeamPlayerIntelligences");

            migrationBuilder.DropColumn(
                name: "DefenseLeaderName",
                table: "TeamPlayerIntelligences");

            migrationBuilder.DropColumn(
                name: "DefenseLeaderRating",
                table: "TeamPlayerIntelligences");

            migrationBuilder.DropColumn(
                name: "KeyPassLeaderCount",
                table: "TeamPlayerIntelligences");

            migrationBuilder.DropColumn(
                name: "KeyPassLeaderName",
                table: "TeamPlayerIntelligences");

            migrationBuilder.DropColumn(
                name: "MidfieldBrainAssists",
                table: "TeamPlayerIntelligences");

            migrationBuilder.DropColumn(
                name: "MidfieldBrainName",
                table: "TeamPlayerIntelligences");

            migrationBuilder.DropColumn(
                name: "MidfieldBrainRating",
                table: "TeamPlayerIntelligences");

            migrationBuilder.DropColumn(
                name: "OneManDependency",
                table: "TeamPlayerIntelligences");

            migrationBuilder.DropColumn(
                name: "ShotsLeaderCount",
                table: "TeamPlayerIntelligences");

            migrationBuilder.DropColumn(
                name: "ShotsLeaderName",
                table: "TeamPlayerIntelligences");

            migrationBuilder.DropColumn(
                name: "TeamTotalGoals",
                table: "TeamPlayerIntelligences");

            migrationBuilder.DropColumn(
                name: "Top2GoalSharePct",
                table: "TeamPlayerIntelligences");

            migrationBuilder.DropColumn(
                name: "TopScorerGoalSharePct",
                table: "TeamPlayerIntelligences");
        }
    }
}
