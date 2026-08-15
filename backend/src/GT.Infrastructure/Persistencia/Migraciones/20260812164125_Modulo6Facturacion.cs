using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GT.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class Modulo6Facturacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FacturaId",
                table: "Viajes",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EmpresaEmisora",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    RazonSocial = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Cuit = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: false),
                    Domicilio = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CondicionIva = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IngresosBrutos = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    InicioActividades = table.Column<DateOnly>(type: "date", nullable: true),
                    PuntoDeVenta = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    Cbu = table.Column<string>(type: "nvarchar(22)", maxLength: 22, nullable: true),
                    Telefono = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    LogoRuta = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    LogoTipoContenido = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LogoNombreOriginal = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmpresaEmisora", x => x.Id);
                    table.CheckConstraint("CK_EmpresaEmisora_FilaUnica", "[Id] = 1");
                });

            migrationBuilder.CreateTable(
                name: "Facturas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NumeroComprobante = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    TipoComprobante = table.Column<byte>(type: "tinyint", nullable: false),
                    TipoFacturacion = table.Column<byte>(type: "tinyint", nullable: false),
                    CondicionDeVenta = table.Column<byte>(type: "tinyint", nullable: false),
                    PeriodoMes = table.Column<byte>(type: "tinyint", nullable: false),
                    PeriodoAnio = table.Column<short>(type: "smallint", nullable: false),
                    Detalle = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ClienteId = table.Column<int>(type: "int", nullable: false),
                    ClienteRazonSocial = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ClienteCuit = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: false),
                    ClienteDomicilio = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EmisorRazonSocial = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EmisorCuit = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: false),
                    EmisorDomicilio = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EmisorCondicionIva = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EmisorIngresosBrutos = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    EmisorInicioActividades = table.Column<DateOnly>(type: "date", nullable: true),
                    EmisorPuntoDeVenta = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    EmisorCbu = table.Column<string>(type: "nvarchar(22)", maxLength: 22, nullable: true),
                    EmisorTelefono = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    EmisorEmail = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    Neto = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Iva = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Cae = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CaeVencimiento = table.Column<DateOnly>(type: "date", nullable: false),
                    VencimientoPago = table.Column<DateOnly>(type: "date", nullable: false),
                    Estado = table.Column<byte>(type: "tinyint", nullable: false),
                    FechaCobro = table.Column<DateOnly>(type: "date", nullable: true),
                    MotivoAnulacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FacturaReemplazadaId = table.Column<int>(type: "int", nullable: true),
                    DocumentoRuta = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Facturas", x => x.Id);
                    table.CheckConstraint("CK_Facturas_PeriodoMes", "[PeriodoMes] BETWEEN 1 AND 12");
                    table.CheckConstraint("CK_Facturas_Total", "[Total] = [Neto] + [Iva]");
                    table.ForeignKey(
                        name: "FK_Facturas_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Facturas_Facturas_FacturaReemplazadaId",
                        column: x => x.FacturaReemplazadaId,
                        principalTable: "Facturas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CambiosDeEstadoFactura",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FacturaId = table.Column<int>(type: "int", nullable: false),
                    EstadoAnterior = table.Column<byte>(type: "tinyint", nullable: true),
                    EstadoNuevo = table.Column<byte>(type: "tinyint", nullable: true),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    OcurridoEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CambiosDeEstadoFactura", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CambiosDeEstadoFactura_Facturas_FacturaId",
                        column: x => x.FacturaId,
                        principalTable: "Facturas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CambiosDeEstadoFactura_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Viajes_FacturaId",
                table: "Viajes",
                column: "FacturaId");

            migrationBuilder.CreateIndex(
                name: "IX_CambiosDeEstadoFactura_FacturaId_OcurridoEn",
                table: "CambiosDeEstadoFactura",
                columns: new[] { "FacturaId", "OcurridoEn" });

            migrationBuilder.CreateIndex(
                name: "IX_CambiosDeEstadoFactura_UsuarioId",
                table: "CambiosDeEstadoFactura",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Facturas_ClienteId",
                table: "Facturas",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Facturas_Estado_VencimientoPago",
                table: "Facturas",
                columns: new[] { "Estado", "VencimientoPago" });

            migrationBuilder.CreateIndex(
                name: "IX_Facturas_FacturaReemplazada",
                table: "Facturas",
                column: "FacturaReemplazadaId",
                unique: true,
                filter: "[FacturaReemplazadaId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Facturas_Fecha_Numero",
                table: "Facturas",
                columns: new[] { "Fecha", "NumeroComprobante" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Facturas_Numero",
                table: "Facturas",
                column: "NumeroComprobante",
                unique: true,
                filter: "[Estado] <> 2");

            migrationBuilder.AddForeignKey(
                name: "FK_Viajes_Facturas_FacturaId",
                table: "Viajes",
                column: "FacturaId",
                principalTable: "Facturas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Viajes_Facturas_FacturaId",
                table: "Viajes");

            migrationBuilder.DropTable(
                name: "CambiosDeEstadoFactura");

            migrationBuilder.DropTable(
                name: "EmpresaEmisora");

            migrationBuilder.DropTable(
                name: "Facturas");

            migrationBuilder.DropIndex(
                name: "IX_Viajes_FacturaId",
                table: "Viajes");

            migrationBuilder.DropColumn(
                name: "FacturaId",
                table: "Viajes");
        }
    }
}
