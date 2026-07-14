using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Transport.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Trajets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LigneBus = table.Column<string>(type: "TEXT", nullable: false),
                    Depart = table.Column<string>(type: "TEXT", nullable: false),
                    Arrivee = table.Column<string>(type: "TEXT", nullable: false),
                    HeureDepart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PlacesDisponibles = table.Column<int>(type: "INTEGER", nullable: false),
                    Statut = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trajets", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Trajets");
        }
    }
}
