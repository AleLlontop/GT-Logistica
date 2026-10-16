using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GT.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class Modulo13Proveedores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Proveedores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    TransportistaId = table.Column<int>(type: "int", nullable: true),
                    RazonSocial = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Cuit = table.Column<string>(type: "varchar(11)", unicode: false, maxLength: 11, nullable: true),
                    Telefono = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    NombreComercial = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Direccion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Proveedores", x => x.Id);
                    table.CheckConstraint("CK_Proveedores_Identidad", "([Tipo] = 0 AND [TransportistaId] IS NOT NULL AND [RazonSocial] IS NULL AND [Cuit] IS NULL AND [Telefono] IS NULL AND [Email] IS NULL) OR ([Tipo] = 1 AND [TransportistaId] IS NULL AND [RazonSocial] IS NOT NULL AND LEN(LTRIM(RTRIM([RazonSocial]))) > 0 AND [Cuit] IS NOT NULL AND LEN([Cuit]) = 11)");
                    table.CheckConstraint("CK_Proveedores_Tipo", "[Tipo] IN (0, 1)");
                    table.CheckConstraint("CK_Proveedores_Version", "[Version] > 0");
                    table.ForeignKey(
                        name: "FK_Proveedores_Transportistas_TransportistaId",
                        column: x => x.TransportistaId,
                        principalTable: "Transportistas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FacturasProveedor",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProveedorId = table.Column<int>(type: "int", nullable: false),
                    TipoProveedorAlRegistrar = table.Column<int>(type: "int", nullable: false),
                    ProveedorRazonSocial = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProveedorCuit = table.Column<string>(type: "varchar(11)", unicode: false, maxLength: 11, nullable: false),
                    Numero = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    Detalle = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Neto = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Iva = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ImporteTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Cae = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaVencimientoCae = table.Column<DateOnly>(type: "date", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    LiquidacionId = table.Column<int>(type: "int", nullable: true),
                    ImporteLiquidacionAlRegistrar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ArchivoRuta = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ArchivoNombre = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ArchivoTipoContenido = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FacturasProveedor", x => x.Id);
                    table.CheckConstraint("CK_FacturasProveedor_Archivo", "([ArchivoRuta] IS NULL AND [ArchivoNombre] IS NULL AND [ArchivoTipoContenido] IS NULL) OR ([ArchivoRuta] IS NOT NULL AND LEN([ArchivoRuta]) > 0 AND [ArchivoNombre] IS NOT NULL AND LEN([ArchivoNombre]) > 0 AND [ArchivoTipoContenido] IS NOT NULL AND [ArchivoTipoContenido] = N'application/pdf')");
                    table.CheckConstraint("CK_FacturasProveedor_Estado", "[Estado] = 0");
                    table.CheckConstraint("CK_FacturasProveedor_Fechas", "[FechaVencimientoCae] >= [Fecha]");
                    table.CheckConstraint("CK_FacturasProveedor_Importes", "[Neto] > 0 AND [Iva] = ROUND([Neto] * 0.21, 2) AND [ImporteTotal] = [Neto] + [Iva]");
                    table.CheckConstraint("CK_FacturasProveedor_Liquidacion", "([TipoProveedorAlRegistrar] = 0 AND [LiquidacionId] IS NOT NULL AND [ImporteLiquidacionAlRegistrar] IS NOT NULL AND [ImporteLiquidacionAlRegistrar] = [ImporteTotal]) OR ([TipoProveedorAlRegistrar] = 1 AND [LiquidacionId] IS NULL AND [ImporteLiquidacionAlRegistrar] IS NULL)");
                    table.CheckConstraint("CK_FacturasProveedor_Textos", "LEN(LTRIM(RTRIM([Numero]))) > 0 AND LEN(LTRIM(RTRIM([Detalle]))) > 0 AND LEN(LTRIM(RTRIM([Cae]))) > 0");
                    table.ForeignKey(
                        name: "FK_FacturasProveedor_Liquidaciones_LiquidacionId",
                        column: x => x.LiquidacionId,
                        principalTable: "Liquidaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FacturasProveedor_Proveedores_ProveedorId",
                        column: x => x.ProveedorId,
                        principalTable: "Proveedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FacturasProveedor_Fecha_Id",
                table: "FacturasProveedor",
                columns: new[] { "Fecha", "Id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_FacturasProveedor_LiquidacionId",
                table: "FacturasProveedor",
                column: "LiquidacionId",
                unique: true,
                filter: "[LiquidacionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FacturasProveedor_ProveedorId_Numero",
                table: "FacturasProveedor",
                columns: new[] { "ProveedorId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Proveedores_Cuit",
                table: "Proveedores",
                column: "Cuit",
                unique: true,
                filter: "[Cuit] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Proveedores_TransportistaId",
                table: "Proveedores",
                column: "TransportistaId",
                unique: true,
                filter: "[TransportistaId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FacturasProveedor");

            migrationBuilder.DropTable(
                name: "Proveedores");
        }
    }
}
