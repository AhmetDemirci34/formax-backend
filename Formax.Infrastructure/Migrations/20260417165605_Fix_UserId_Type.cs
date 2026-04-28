using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    public partial class Fix_UserId_Type : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 🔥 1. PK DROP (KRİTİK)
            migrationBuilder.DropPrimaryKey(
                name: "PK_UserStats",
                table: "UserStats");

            // 🔥 2. COLUMN TYPE CHANGE
            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "UserStats",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            // 🔥 3. VIEW COUNT EKLE
            migrationBuilder.AddColumn<int>(
                name: "ViewCount",
                table: "UserStats",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // 🔥 4. PK RE-ADD
            migrationBuilder.AddPrimaryKey(
                name: "PK_UserStats",
                table: "UserStats",
                column: "UserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 🔥 1. PK DROP
            migrationBuilder.DropPrimaryKey(
                name: "PK_UserStats",
                table: "UserStats");

            // 🔥 2. VIEW COUNT REMOVE
            migrationBuilder.DropColumn(
                name: "ViewCount",
                table: "UserStats");

            // 🔥 3. TYPE BACK TO STRING
            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "UserStats",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            // 🔥 4. PK RE-ADD
            migrationBuilder.AddPrimaryKey(
                name: "PK_UserStats",
                table: "UserStats",
                column: "UserId");
        }
    }
}