using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PhaseAwareStandingsAndSeasonVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhaseResolution",
                table: "LeagueStandingsSnapshots",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ScopePhase",
                table: "LeagueStandingsSnapshots",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "UnresolvedPhaseFixtures",
                table: "LeagueStandingsSnapshots",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "VerificationStatus",
                table: "LeagueSeasons",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            // MEVCUT DOGRULANMIS KAYITLAR: hepsinin resmi kaynagi ve VerifiedAtUtc'si var,
            // dolayisiyla "Confirmed" olarak isaretlenir. Bos string birakmak, dogrulanmis
            // kayitlari "durumu bilinmiyor" gibi gosterirdi.
            migrationBuilder.Sql(
                "UPDATE [LeagueSeasons] SET [VerificationStatus] = N'Confirmed' " +
                "WHERE [VerificationStatus] IS NULL OR [VerificationStatus] = N'';");

            // ESKI UEFA SNAPSHOT'LARI GECERSIZ: eleme maclarindan uretilmis tablolar
            // kullaniciya sunulamaz. Silinir; asama-duyarli hesap onlari yeniden uretir.
            // Ulusal lig snapshot'larina DOKUNULMAZ.
            migrationBuilder.Sql(
                "DELETE FROM [LeagueStandingsSnapshots] WHERE [LeagueId] IN (2, 3, 848);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhaseResolution",
                table: "LeagueStandingsSnapshots");

            migrationBuilder.DropColumn(
                name: "ScopePhase",
                table: "LeagueStandingsSnapshots");

            migrationBuilder.DropColumn(
                name: "UnresolvedPhaseFixtures",
                table: "LeagueStandingsSnapshots");

            migrationBuilder.DropColumn(
                name: "VerificationStatus",
                table: "LeagueSeasons");
        }
    }
}
