using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academique.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmploisDuTemps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EtudiantId = table.Column<int>(type: "INTEGER", nullable: false),
                    Jour = table.Column<string>(type: "TEXT", nullable: false),
                    Creneau = table.Column<string>(type: "TEXT", nullable: false),
                    Matiere = table.Column<string>(type: "TEXT", nullable: false),
                    Salle = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmploisDuTemps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Notes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EtudiantId = table.Column<int>(type: "INTEGER", nullable: false),
                    Matiere = table.Column<string>(type: "TEXT", nullable: false),
                    Valeur = table.Column<double>(type: "REAL", nullable: false),
                    Semestre = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notes", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmploisDuTemps");

            migrationBuilder.DropTable(
                name: "Notes");
        }
    }
}
