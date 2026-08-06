using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GT.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class Modulo3Choferes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentacionTipos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DiasAvisoVencimiento = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentacionTipos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Transportistas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Cuit = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: false),
                    Tipo = table.Column<byte>(type: "tinyint", nullable: false),
                    Telefono = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transportistas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Choferes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PersonaId = table.Column<int>(type: "int", nullable: false),
                    Cuil = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: false),
                    TransportistaId = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Choferes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Choferes_Personas_PersonaId",
                        column: x => x.PersonaId,
                        principalTable: "Personas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Choferes_Transportistas_TransportistaId",
                        column: x => x.TransportistaId,
                        principalTable: "Transportistas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Documentaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChoferId = table.Column<int>(type: "int", nullable: false),
                    DocumentacionTipoId = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FechaEmision = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaVencimiento = table.Column<DateOnly>(type: "date", nullable: false),
                    ArchivoRuta = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ArchivoNombre = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ArchivoTipoContenido = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documentaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Documentaciones_Choferes_ChoferId",
                        column: x => x.ChoferId,
                        principalTable: "Choferes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Documentaciones_DocumentacionTipos_DocumentacionTipoId",
                        column: x => x.DocumentacionTipoId,
                        principalTable: "DocumentacionTipos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Choferes_Cuil",
                table: "Choferes",
                column: "Cuil",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Choferes_PersonaId",
                table: "Choferes",
                column: "PersonaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Choferes_TransportistaId",
                table: "Choferes",
                column: "TransportistaId");

            migrationBuilder.CreateIndex(
                name: "IX_Documentaciones_ChoferId_TipoId_Vencimiento",
                table: "Documentaciones",
                columns: new[] { "ChoferId", "DocumentacionTipoId", "FechaVencimiento" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Documentaciones_DocumentacionTipoId",
                table: "Documentaciones",
                column: "DocumentacionTipoId");

            migrationBuilder.CreateIndex(
                name: "IX_Documentaciones_FechaVencimiento",
                table: "Documentaciones",
                column: "FechaVencimiento");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentacionTipos_Nombre",
                table: "DocumentacionTipos",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transportistas_Cuit",
                table: "Transportistas",
                column: "Cuit",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Documentaciones");

            migrationBuilder.DropTable(
                name: "Choferes");

            migrationBuilder.DropTable(
                name: "DocumentacionTipos");

            migrationBuilder.DropTable(
                name: "Transportistas");
        }
    }
}
