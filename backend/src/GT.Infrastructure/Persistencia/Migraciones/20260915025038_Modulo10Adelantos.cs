using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GT.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class Modulo10Adelantos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Adelantos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PersonaId = table.Column<int>(type: "int", nullable: false),
                    TipoBeneficiario = table.Column<byte>(type: "tinyint", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Importe = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Estado = table.Column<byte>(type: "tinyint", nullable: false),
                    MotivoRechazo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MotivoAnulacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Adelantos", x => x.Id);
                    table.CheckConstraint("CK_Adelantos_Estado", "([Estado] IN (0, 1) AND [MotivoRechazo] IS NULL AND [MotivoAnulacion] IS NULL) OR ([Estado] = 2 AND [MotivoRechazo] IS NOT NULL AND LEN([MotivoRechazo]) > 0 AND [MotivoAnulacion] IS NULL) OR ([Estado] = 3 AND [MotivoAnulacion] IS NOT NULL AND LEN([MotivoAnulacion]) > 0 AND [MotivoRechazo] IS NULL)");
                    table.CheckConstraint("CK_Adelantos_Importe", "[Importe] > 0");
                    table.CheckConstraint("CK_Adelantos_Motivo", "LEN([Motivo]) > 0");
                    table.CheckConstraint("CK_Adelantos_TipoBeneficiario", "[TipoBeneficiario] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_Adelantos_Personas_PersonaId",
                        column: x => x.PersonaId,
                        principalTable: "Personas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CambiosDeAdelanto",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdelantoId = table.Column<int>(type: "int", nullable: false),
                    Operacion = table.Column<byte>(type: "tinyint", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    OcurridoEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CambiosDeAdelanto", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CambiosDeAdelanto_Adelantos_AdelantoId",
                        column: x => x.AdelantoId,
                        principalTable: "Adelantos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CambiosDeAdelanto_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Adelantos_Estado",
                table: "Adelantos",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_Adelantos_Fecha",
                table: "Adelantos",
                columns: new[] { "Fecha", "Id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Adelantos_PersonaId",
                table: "Adelantos",
                column: "PersonaId");

            migrationBuilder.CreateIndex(
                name: "IX_CambiosDeAdelanto_Operacion",
                table: "CambiosDeAdelanto",
                columns: new[] { "AdelantoId", "Operacion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CambiosDeAdelanto_UsuarioId",
                table: "CambiosDeAdelanto",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CambiosDeAdelanto");

            migrationBuilder.DropTable(
                name: "Adelantos");
        }
    }
}
