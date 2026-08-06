using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GT.Infrastructure.Persistencia.Migraciones
{
    /// <summary>
    /// Esquema del Módulo 2: la tabla <c>Personas</c> y las cinco columnas nuevas de <c>Usuarios</c>.
    ///
    /// El orden de los pasos no es decorativo. El andamiaje que genera <c>dotnet ef</c> agrega las
    /// columnas obligatorias con valor por defecto vacío y crea los índices únicos encima, lo que
    /// dejaría al usuario <c>admin</c> —la única cuenta preexistente, garantizada por FR-019 del
    /// Módulo 1— con un email vacío, incumpliendo FR-003. Por eso acá se agregan nullables, se
    /// rellena la fila que ya existe, y recién entonces se vuelven obligatorias y se indexan
    /// (data-model.md §Migración, research §5).
    ///
    /// No siembra ninguna persona: el padrón arranca vacío y se completa desde la pantalla del
    /// módulo (FR-024).
    /// </summary>
    public partial class PersonasYDatosDeUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Paso 1: la tabla Personas, vacía ───────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "Personas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Apellido = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Dni = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    Tipo = table.Column<byte>(type: "tinyint", nullable: false),
                    Telefono = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    FechaNacimiento = table.Column<DateOnly>(type: "date", nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Personas", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Personas_Dni",
                table: "Personas",
                column: "Dni",
                unique: true);

            // ── Paso 2: columnas nuevas en Usuarios, nullables por ahora ───────────────────────
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Usuarios",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailNormalizado",
                table: "Usuarios",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaAlta",
                table: "Usuarios",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordActualizadaEn",
                table: "Usuarios",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PersonaId",
                table: "Usuarios",
                type: "int",
                nullable: true);

            // ── Paso 3: rellenar las cuentas que ya existían ───────────────────────────────────
            // En la práctica es una sola fila, `admin`, y le queda `admin@gtlogistica.local`. El
            // dominio `.local` no existe, así que no se le puede mandar un correo por accidente; el
            // responsable de sistemas la corrige desde la pantalla nueva. Derivar la dirección del
            // username en vez de escribirla a mano mantiene la unicidad aunque la instalación tuviera
            // más de una cuenta.
            // Va dentro de EXEC a propósito: SQL Server compila el lote entero antes de ejecutarlo,
            // así que un UPDATE escrito directo acá fallaría con "Invalid column name 'Email'" —las
            // columnas se agregan en este mismo lote y todavía no existen en tiempo de compilación—.
            // El EXEC difiere la compilación hasta que ya fueron creadas.
            migrationBuilder.Sql(
                """
                EXEC(N'
                    UPDATE Usuarios
                    SET Email                 = LOWER(Username) + ''@gtlogistica.local'',
                        EmailNormalizado      = LOWER(Username) + ''@gtlogistica.local'',
                        FechaAlta             = SYSUTCDATETIME(),
                        PasswordActualizadaEn = SYSUTCDATETIME()
                    WHERE Email IS NULL;
                ');
                """);

            // ── Paso 4: recién ahora son obligatorias ──────────────────────────────────────────
            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Usuarios",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(254)",
                oldMaxLength: 254,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EmailNormalizado",
                table: "Usuarios",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(254)",
                oldMaxLength: 254,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "FechaAlta",
                table: "Usuarios",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "PasswordActualizadaEn",
                table: "Usuarios",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            // ── Paso 5: índices y clave foránea ───────────────────────────────────────────────
            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_EmailNormalizado",
                table: "Usuarios",
                column: "EmailNormalizado",
                unique: true);

            // Índice FILTRADO: sin el filtro, SQL Server considera duplicados a los varios `NULL` y
            // sólo un usuario podría quedarse sin persona asociada, que es un caso válido (FR-008).
            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_PersonaId",
                table: "Usuarios",
                column: "PersonaId",
                unique: true,
                filter: "[PersonaId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Usuarios_Personas_PersonaId",
                table: "Usuarios",
                column: "PersonaId",
                principalTable: "Personas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Usuarios_Personas_PersonaId",
                table: "Usuarios");

            migrationBuilder.DropIndex(
                name: "IX_Usuarios_EmailNormalizado",
                table: "Usuarios");

            migrationBuilder.DropIndex(
                name: "IX_Usuarios_PersonaId",
                table: "Usuarios");

            migrationBuilder.DropTable(
                name: "Personas");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "EmailNormalizado",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "FechaAlta",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "PasswordActualizadaEn",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "PersonaId",
                table: "Usuarios");
        }
    }
}
