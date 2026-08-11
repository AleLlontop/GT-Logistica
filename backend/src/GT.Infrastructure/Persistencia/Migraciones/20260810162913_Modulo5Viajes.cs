using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GT.Infrastructure.Persistencia.Migraciones
{
    /// <summary>
    /// Módulo 5: una secuencia y tres tablas nuevas.
    ///
    /// <b>No modifica ninguna tabla existente.</b> Es el primer módulo que se apoya sobre dos módulos
    /// de negocio anteriores sin agregarles una columna ni una navegación: a <c>Choferes</c>,
    /// <c>Vehiculos</c> y <c>Transportistas</c> los referencia con claves foráneas y nada más.
    ///
    /// <b>No siembra ninguna fila de negocio</b>: los dos padrones arrancan vacíos y la numeración de
    /// viajes en 1. Los dos permisos nuevos y su reparto por rol los agrega <c>SembradorInicial</c>,
    /// que ya corre en cada arranque y es idempotente.
    ///
    /// <b>Es reversible</b>: el <c>Down</c> borra las tres tablas y la secuencia, y todo lo de los
    /// Módulos 1 a 4 queda exactamente como estaba.
    /// </summary>
    public partial class Modulo5Viajes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "NumeroDeViaje");

            // **`NO CACHE` es el punto de la decisión, no un detalle** (research §1). Sin él, un
            // apagado sucio del motor hace saltar la numeración: el viaje siguiente al 12 pasaría a
            // ser el 1012, contra lo que piden FR-011 y el escenario US2 esc. 5. Y en un entorno que
            // se levanta y baja con `compose`, un apagado sucio no es hipotético.
            //
            // El costo es una escritura de log por número, invisible a este volumen —decenas de
            // viajes por semana—.
            //
            // Va como SQL porque `CreateSequence` no expone la opción; la secuencia se declara igual
            // en el modelo para que EF la conozca.
            migrationBuilder.Sql("ALTER SEQUENCE [dbo].[NumeroDeViaje] NO CACHE;");

            migrationBuilder.CreateTable(
                name: "Clientes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RazonSocial = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Cuit = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: false),
                    Telefono = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    Direccion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clientes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Viajes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Numero = table.Column<int>(type: "int", nullable: false, defaultValueSql: "NEXT VALUE FOR dbo.NumeroDeViaje"),
                    ClienteId = table.Column<int>(type: "int", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    Origen = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Destino = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NumeroRemito = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DetalleCarga = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Importe = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Estado = table.Column<byte>(type: "tinyint", nullable: false),
                    MotivoAnulacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ChoferId = table.Column<int>(type: "int", nullable: true),
                    VehiculoId = table.Column<int>(type: "int", nullable: true),
                    TransportistaId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Viajes", x => x.Id);
                    table.CheckConstraint("CK_Viajes_Importe", "[Importe] >= 0");
                    table.ForeignKey(
                        name: "FK_Viajes_Choferes_ChoferId",
                        column: x => x.ChoferId,
                        principalTable: "Choferes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Viajes_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Viajes_Transportistas_TransportistaId",
                        column: x => x.TransportistaId,
                        principalTable: "Transportistas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Viajes_Vehiculos_VehiculoId",
                        column: x => x.VehiculoId,
                        principalTable: "Vehiculos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CambiosDeEstadoViaje",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ViajeId = table.Column<int>(type: "int", nullable: false),
                    EstadoAnterior = table.Column<byte>(type: "tinyint", nullable: true),
                    EstadoNuevo = table.Column<byte>(type: "tinyint", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    OcurridoEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CambiosDeEstadoViaje", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CambiosDeEstadoViaje_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CambiosDeEstadoViaje_Viajes_ViajeId",
                        column: x => x.ViajeId,
                        principalTable: "Viajes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CambiosDeEstadoViaje_UsuarioId",
                table: "CambiosDeEstadoViaje",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_CambiosDeEstadoViaje_ViajeId_OcurridoEn",
                table: "CambiosDeEstadoViaje",
                columns: new[] { "ViajeId", "OcurridoEn" });

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_Cuit",
                table: "Clientes",
                column: "Cuit",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Viajes_ChoferEnCurso",
                table: "Viajes",
                column: "ChoferId",
                unique: true,
                filter: "[ChoferId] IS NOT NULL AND [Estado] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Viajes_ClienteId",
                table: "Viajes",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Viajes_Estado",
                table: "Viajes",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_Viajes_Fecha_Numero",
                table: "Viajes",
                columns: new[] { "Fecha", "Numero" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Viajes_Numero",
                table: "Viajes",
                column: "Numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Viajes_NumeroRemito",
                table: "Viajes",
                column: "NumeroRemito",
                unique: true,
                filter: "[NumeroRemito] IS NOT NULL AND [Estado] <> 3");

            migrationBuilder.CreateIndex(
                name: "IX_Viajes_TransportistaId",
                table: "Viajes",
                column: "TransportistaId");

            migrationBuilder.CreateIndex(
                name: "IX_Viajes_VehiculoEnCurso",
                table: "Viajes",
                column: "VehiculoId",
                unique: true,
                filter: "[VehiculoId] IS NOT NULL AND [Estado] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CambiosDeEstadoViaje");

            migrationBuilder.DropTable(
                name: "Viajes");

            migrationBuilder.DropTable(
                name: "Clientes");

            migrationBuilder.DropSequence(
                name: "NumeroDeViaje");
        }
    }
}
