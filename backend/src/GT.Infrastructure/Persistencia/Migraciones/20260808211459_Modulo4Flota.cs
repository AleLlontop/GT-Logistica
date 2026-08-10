using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GT.Infrastructure.Persistencia.Migraciones
{
    /// <summary>
    /// Módulo 4: tres tablas nuevas y una columna agregada a una existente.
    ///
    /// No siembra ningún dato de negocio: el catálogo de tipos de vehículo y el padrón de flota se
    /// completan desde las pantallas del módulo (US1, US2). Los dos permisos nuevos los siembra
    /// <c>SembradorInicial</c>, que es idempotente y ya corre en cada arranque.
    ///
    /// <b>Es reversible</b>: el <c>Down</c> borra las tres tablas nuevas y quita la columna
    /// <c>Ambito</c>, y los datos del Módulo 3 quedan exactamente como estaban.
    /// </summary>
    public partial class Modulo4Flota : Migration
    {
        /// <summary>
        /// <c>DocumentacionAmbito.Chofer</c>. Va escrito acá y no como referencia al enum porque una
        /// migración describe el esquema del momento en que se escribió: si el enum cambiara sus
        /// valores más adelante, esta migración tiene que seguir haciendo lo mismo.
        /// </summary>
        private const byte AmbitoChofer = 1;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // FR-017c: **todos los tipos ya cargados quedan con ámbito chofer**, así que ningún
            // documento existente cambia de comportamiento. El valor por defecto va en la misma
            // sentencia que crea la columna, de modo que no hace falta ninguna corrección manual ni
            // ningún tratamiento de excepciones.
            migrationBuilder.AddColumn<byte>(
                name: "Ambito",
                table: "DocumentacionTipos",
                type: "tinyint",
                nullable: false,
                defaultValue: AmbitoChofer);

            migrationBuilder.CreateTable(
                name: "TiposVehiculo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TiposVehiculo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Vehiculos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Patente = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Marca = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Modelo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TipoVehiculoId = table.Column<int>(type: "int", nullable: false),
                    TransportistaId = table.Column<int>(type: "int", nullable: false),
                    EstadoOperativo = table.Column<byte>(type: "tinyint", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehiculos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vehiculos_TiposVehiculo_TipoVehiculoId",
                        column: x => x.TipoVehiculoId,
                        principalTable: "TiposVehiculo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Vehiculos_Transportistas_TransportistaId",
                        column: x => x.TransportistaId,
                        principalTable: "Transportistas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentacionesVehiculo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VehiculoId = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_DocumentacionesVehiculo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentacionesVehiculo_DocumentacionTipos_DocumentacionTipoId",
                        column: x => x.DocumentacionTipoId,
                        principalTable: "DocumentacionTipos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentacionesVehiculo_Vehiculos_VehiculoId",
                        column: x => x.VehiculoId,
                        principalTable: "Vehiculos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentacionesVehiculo_DocumentacionTipoId",
                table: "DocumentacionesVehiculo",
                column: "DocumentacionTipoId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentacionesVehiculo_FechaVencimiento",
                table: "DocumentacionesVehiculo",
                column: "FechaVencimiento");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentacionesVehiculo_VehiculoId_TipoId_Vencimiento",
                table: "DocumentacionesVehiculo",
                columns: new[] { "VehiculoId", "DocumentacionTipoId", "FechaVencimiento" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_TiposVehiculo_Nombre",
                table: "TiposVehiculo",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehiculos_Patente",
                table: "Vehiculos",
                column: "Patente",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehiculos_TipoVehiculoId",
                table: "Vehiculos",
                column: "TipoVehiculoId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehiculos_TransportistaId",
                table: "Vehiculos",
                column: "TransportistaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentacionesVehiculo");

            migrationBuilder.DropTable(
                name: "Vehiculos");

            migrationBuilder.DropTable(
                name: "TiposVehiculo");

            migrationBuilder.DropColumn(
                name: "Ambito",
                table: "DocumentacionTipos");
        }
    }
}
