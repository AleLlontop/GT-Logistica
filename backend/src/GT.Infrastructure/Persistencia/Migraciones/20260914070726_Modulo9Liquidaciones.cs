using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GT.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class Modulo9Liquidaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "NumeroDeLiquidacion");

            migrationBuilder.CreateSequence<int>(
                name: "NumeroDeOrdenDePago");

            // El mismo `NO CACHE` que el número de viaje (Módulo 5, research §1): sin él, un apagado sucio
            // del motor hace saltar la numeración y la liquidación siguiente a la LQ-12 sería la LQ-1012.
            // Va como SQL porque `CreateSequence` no expone la opción.
            migrationBuilder.Sql("ALTER SEQUENCE [dbo].[NumeroDeLiquidacion] NO CACHE;");
            migrationBuilder.Sql("ALTER SEQUENCE [dbo].[NumeroDeOrdenDePago] NO CACHE;");

            migrationBuilder.CreateTable(
                name: "Liquidaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Numero = table.Column<int>(type: "int", nullable: false, defaultValueSql: "NEXT VALUE FOR dbo.NumeroDeLiquidacion"),
                    TransportistaId = table.Column<int>(type: "int", nullable: false),
                    PeriodoMes = table.Column<byte>(type: "tinyint", nullable: false),
                    PeriodoAnio = table.Column<short>(type: "smallint", nullable: false),
                    ImporteTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ImportePagado = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    Estado = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    MotivoAnulacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Liquidaciones", x => x.Id);
                    table.CheckConstraint("CK_Liquidaciones_Estado", "([Estado] = 0 AND [ImportePagado] < [ImporteTotal] AND [MotivoAnulacion] IS NULL) OR ([Estado] = 1 AND [ImportePagado] = [ImporteTotal] AND [MotivoAnulacion] IS NULL) OR ([Estado] = 2 AND [ImportePagado] = 0 AND [MotivoAnulacion] IS NOT NULL)");
                    table.CheckConstraint("CK_Liquidaciones_ImportePagado", "[ImportePagado] >= 0 AND [ImportePagado] <= [ImporteTotal]");
                    table.CheckConstraint("CK_Liquidaciones_ImporteTotal", "[ImporteTotal] > 0");
                    table.CheckConstraint("CK_Liquidaciones_PeriodoMes", "[PeriodoMes] BETWEEN 1 AND 12");
                    table.ForeignKey(
                        name: "FK_Liquidaciones_Transportistas_TransportistaId",
                        column: x => x.TransportistaId,
                        principalTable: "Transportistas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CambiosDeLiquidacion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LiquidacionId = table.Column<int>(type: "int", nullable: false),
                    Operacion = table.Column<byte>(type: "tinyint", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    OcurridoEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CambiosDeLiquidacion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CambiosDeLiquidacion_Liquidaciones_LiquidacionId",
                        column: x => x.LiquidacionId,
                        principalTable: "Liquidaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CambiosDeLiquidacion_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LiquidacionViajes",
                columns: table => new
                {
                    LiquidacionId = table.Column<int>(type: "int", nullable: false),
                    ViajeId = table.Column<int>(type: "int", nullable: false),
                    Vigente = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiquidacionViajes", x => new { x.LiquidacionId, x.ViajeId });
                    table.ForeignKey(
                        name: "FK_LiquidacionViajes_Liquidaciones_LiquidacionId",
                        column: x => x.LiquidacionId,
                        principalTable: "Liquidaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LiquidacionViajes_Viajes_ViajeId",
                        column: x => x.ViajeId,
                        principalTable: "Viajes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrdenesDePago",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Numero = table.Column<int>(type: "int", nullable: false, defaultValueSql: "NEXT VALUE FOR dbo.NumeroDeOrdenDePago"),
                    LiquidacionId = table.Column<int>(type: "int", nullable: false),
                    FechaPago = table.Column<DateOnly>(type: "date", nullable: false),
                    Importe = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    RegistradaEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrdenesDePago", x => x.Id);
                    table.CheckConstraint("CK_OrdenesDePago_Importe", "[Importe] > 0");
                    table.ForeignKey(
                        name: "FK_OrdenesDePago_Liquidaciones_LiquidacionId",
                        column: x => x.LiquidacionId,
                        principalTable: "Liquidaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrdenesDePago_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CambiosDeLiquidacionViajes",
                columns: table => new
                {
                    CambioDeLiquidacionId = table.Column<int>(type: "int", nullable: false),
                    ViajeId = table.Column<int>(type: "int", nullable: false),
                    Agregado = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CambiosDeLiquidacionViajes", x => new { x.CambioDeLiquidacionId, x.ViajeId });
                    table.ForeignKey(
                        name: "FK_CambiosDeLiquidacionViajes_CambiosDeLiquidacion_CambioDeLiquidacionId",
                        column: x => x.CambioDeLiquidacionId,
                        principalTable: "CambiosDeLiquidacion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CambiosDeLiquidacionViajes_Viajes_ViajeId",
                        column: x => x.ViajeId,
                        principalTable: "Viajes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CambiosDeLiquidacion_Unica",
                table: "CambiosDeLiquidacion",
                columns: new[] { "LiquidacionId", "Operacion" },
                unique: true,
                filter: "[Operacion] <> 1");

            migrationBuilder.CreateIndex(
                name: "IX_CambiosDeLiquidacion_UsuarioId",
                table: "CambiosDeLiquidacion",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_CambiosDeLiquidacionViajes_ViajeId",
                table: "CambiosDeLiquidacionViajes",
                column: "ViajeId");

            migrationBuilder.CreateIndex(
                name: "IX_Liquidaciones_Estado",
                table: "Liquidaciones",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_Liquidaciones_Numero",
                table: "Liquidaciones",
                column: "Numero",
                unique: true,
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Liquidaciones_Periodo",
                table: "Liquidaciones",
                columns: new[] { "PeriodoAnio", "PeriodoMes" });

            migrationBuilder.CreateIndex(
                name: "IX_Liquidaciones_TransportistaId",
                table: "Liquidaciones",
                column: "TransportistaId");

            migrationBuilder.CreateIndex(
                name: "IX_LiquidacionViajes_ViajeVigente",
                table: "LiquidacionViajes",
                column: "ViajeId",
                unique: true,
                filter: "[Vigente] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_OrdenesDePago_LiquidacionId",
                table: "OrdenesDePago",
                column: "LiquidacionId");

            migrationBuilder.CreateIndex(
                name: "IX_OrdenesDePago_Numero",
                table: "OrdenesDePago",
                column: "Numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrdenesDePago_UsuarioId",
                table: "OrdenesDePago",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CambiosDeLiquidacionViajes");

            migrationBuilder.DropTable(
                name: "LiquidacionViajes");

            migrationBuilder.DropTable(
                name: "OrdenesDePago");

            migrationBuilder.DropTable(
                name: "CambiosDeLiquidacion");

            migrationBuilder.DropTable(
                name: "Liquidaciones");

            migrationBuilder.DropSequence(
                name: "NumeroDeLiquidacion");

            migrationBuilder.DropSequence(
                name: "NumeroDeOrdenDePago");
        }
    }
}
