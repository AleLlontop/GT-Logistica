using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GT.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class Modulo11Caja : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Cajas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SaldoInicial = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FechaApertura = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioResponsableId = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<byte>(type: "tinyint", nullable: false),
                    FechaCierre = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SaldoFinal = table.Column<decimal>(type: "decimal(18,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cajas", x => x.Id);
                    table.CheckConstraint("CK_Cajas_CierreConsistente", "([Estado] = 0 AND [FechaCierre] IS NULL AND [SaldoFinal] IS NULL) OR ([Estado] = 1 AND [FechaCierre] IS NOT NULL AND [SaldoFinal] IS NOT NULL)");
                    table.CheckConstraint("CK_Cajas_SaldoInicial", "[SaldoInicial] >= 0");
                    table.ForeignKey(
                        name: "FK_Cajas_Usuarios_UsuarioResponsableId",
                        column: x => x.UsuarioResponsableId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MovimientosDeCaja",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CajaId = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<byte>(type: "tinyint", nullable: false),
                    Importe = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Concepto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FacturaId = table.Column<int>(type: "int", nullable: true),
                    OrdenDePagoId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientosDeCaja", x => x.Id);
                    table.CheckConstraint("CK_MovimientosDeCaja_Concepto", "LEN(LTRIM(RTRIM([Concepto]))) > 0");
                    table.CheckConstraint("CK_MovimientosDeCaja_Importe", "[Importe] > 0");
                    table.CheckConstraint("CK_MovimientosDeCaja_Referencia", "([Tipo] = 0 AND [OrdenDePagoId] IS NULL) OR ([Tipo] = 1 AND [FacturaId] IS NULL)");
                    table.ForeignKey(
                        name: "FK_MovimientosDeCaja_Cajas_CajaId",
                        column: x => x.CajaId,
                        principalTable: "Cajas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimientosDeCaja_Facturas_FacturaId",
                        column: x => x.FacturaId,
                        principalTable: "Facturas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimientosDeCaja_OrdenesDePago_OrdenDePagoId",
                        column: x => x.OrdenDePagoId,
                        principalTable: "OrdenesDePago",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimientosDeCaja_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cajas_FechaApertura",
                table: "Cajas",
                columns: new[] { "FechaApertura", "Id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Cajas_UsuarioResponsable_Abierta",
                table: "Cajas",
                column: "UsuarioResponsableId",
                unique: true,
                filter: "[Estado] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosDeCaja_Caja_Fecha",
                table: "MovimientosDeCaja",
                columns: new[] { "CajaId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosDeCaja_FacturaId",
                table: "MovimientosDeCaja",
                column: "FacturaId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosDeCaja_Fecha",
                table: "MovimientosDeCaja",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosDeCaja_OrdenDePagoId",
                table: "MovimientosDeCaja",
                column: "OrdenDePagoId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosDeCaja_UsuarioId",
                table: "MovimientosDeCaja",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MovimientosDeCaja");

            migrationBuilder.DropTable(
                name: "Cajas");
        }
    }
}
