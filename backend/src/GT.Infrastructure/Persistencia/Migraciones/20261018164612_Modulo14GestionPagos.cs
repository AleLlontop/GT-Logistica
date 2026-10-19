using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GT.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class Modulo14GestionPagos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MovimientosDeCaja_Referencia",
                table: "MovimientosDeCaja");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FacturasProveedor_Estado",
                table: "FacturasProveedor");

            migrationBuilder.DropIndex(
                name: "IX_CambiosDeLiquidacion_Unica",
                table: "CambiosDeLiquidacion");

            migrationBuilder.CreateSequence<int>(
                name: "NumeroDeOrdenPagoProveedor");
            migrationBuilder.Sql("ALTER SEQUENCE dbo.NumeroDeOrdenPagoProveedor NO CACHE;");

            migrationBuilder.AddColumn<int>(
                name: "OrdenPagoProveedorId",
                table: "OrdenesDePago",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RevertidaEn",
                table: "OrdenesDePago",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RevertidaPorId",
                table: "OrdenesDePago",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Vigente",
                table: "OrdenesDePago",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "OrdenPagoProveedorId",
                table: "MovimientosDeCaja",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrdenDePagoId",
                table: "CambiosDeLiquidacion",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Bancos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Version = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bancos", x => x.Id);
                    table.CheckConstraint("CK_Bancos_Nombre", "DATALENGTH([Nombre]) > 0 AND DATALENGTH([Nombre]) <= 400 AND DATALENGTH([Nombre]) = DATALENGTH(TRIM(NCHAR(9)+NCHAR(10)+NCHAR(11)+NCHAR(12)+NCHAR(13)+NCHAR(32)+NCHAR(133)+NCHAR(160)+NCHAR(5760)+NCHAR(8192)+NCHAR(8193)+NCHAR(8194)+NCHAR(8195)+NCHAR(8196)+NCHAR(8197)+NCHAR(8198)+NCHAR(8199)+NCHAR(8200)+NCHAR(8201)+NCHAR(8202)+NCHAR(8232)+NCHAR(8233)+NCHAR(8239)+NCHAR(8287)+NCHAR(12288) FROM [Nombre] COLLATE Latin1_General_100_BIN2))");
                });

            migrationBuilder.CreateTable(
                name: "OrdenesPagoProveedor",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Numero = table.Column<int>(type: "int", nullable: false, defaultValueSql: "NEXT VALUE FOR dbo.NumeroDeOrdenPagoProveedor"),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    Detalle = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProveedorId = table.Column<int>(type: "int", nullable: false),
                    TipoProveedorAlRegistrar = table.Column<int>(type: "int", nullable: false),
                    ProveedorRazonSocial = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProveedorCuit = table.Column<string>(type: "varchar(11)", unicode: false, maxLength: 11, nullable: false),
                    ProveedorActivoAlGenerar = table.Column<bool>(type: "bit", nullable: false),
                    ImporteTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Estado = table.Column<byte>(type: "tinyint", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    RegistradaEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MotivoAnulacion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AnuladaPorId = table.Column<int>(type: "int", nullable: true),
                    AnuladaEn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrdenesPagoProveedor", x => x.Id);
                    table.CheckConstraint("CK_OPP_Estado", "([Estado]=0 AND [MotivoAnulacion] IS NULL AND [AnuladaPorId] IS NULL AND [AnuladaEn] IS NULL) OR ([Estado]=1 AND [MotivoAnulacion] IS NOT NULL AND LEN(LTRIM(RTRIM([MotivoAnulacion])))>0 AND [AnuladaPorId] IS NOT NULL AND [AnuladaEn] IS NOT NULL)");
                    table.CheckConstraint("CK_OPP_Importe", "[ImporteTotal] > 0");
                    table.ForeignKey(
                        name: "FK_OrdenesPagoProveedor_Proveedores_ProveedorId",
                        column: x => x.ProveedorId,
                        principalTable: "Proveedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrdenesPagoProveedor_Usuarios_AnuladaPorId",
                        column: x => x.AnuladaPorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrdenesPagoProveedor_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MediosPagoProveedor",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrdenPagoProveedorId = table.Column<int>(type: "int", nullable: false),
                    RenglonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<byte>(type: "tinyint", nullable: false),
                    Importe = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    BancoId = table.Column<int>(type: "int", nullable: true),
                    NumeroCheque = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true, collation: "Latin1_General_100_BIN2"),
                    FechaVencimiento = table.Column<DateOnly>(type: "date", nullable: true),
                    DetalleCheque = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstadoCheque = table.Column<byte>(type: "tinyint", nullable: true),
                    CbuOrigen = table.Column<string>(type: "varchar(22)", unicode: false, maxLength: 22, nullable: true),
                    CbuDestino = table.Column<string>(type: "varchar(22)", unicode: false, maxLength: 22, nullable: true),
                    NumeroComprobante = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Observaciones = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediosPagoProveedor", x => x.Id);
                    table.UniqueConstraint("AK_MediosPagoProveedor_Id_Tipo", x => new { x.Id, x.Tipo });
                    table.CheckConstraint("CK_Cheque_Numero", "[Tipo]<>0 OR ([NumeroCheque] IS NOT NULL AND DATALENGTH([NumeroCheque]) > 0 AND DATALENGTH([NumeroCheque]) <= 400 AND DATALENGTH([NumeroCheque]) = DATALENGTH(TRIM(NCHAR(9)+NCHAR(10)+NCHAR(11)+NCHAR(12)+NCHAR(13)+NCHAR(32)+NCHAR(133)+NCHAR(160)+NCHAR(5760)+NCHAR(8192)+NCHAR(8193)+NCHAR(8194)+NCHAR(8195)+NCHAR(8196)+NCHAR(8197)+NCHAR(8198)+NCHAR(8199)+NCHAR(8200)+NCHAR(8201)+NCHAR(8202)+NCHAR(8232)+NCHAR(8233)+NCHAR(8239)+NCHAR(8287)+NCHAR(12288) FROM [NumeroCheque] COLLATE Latin1_General_100_BIN2)))");
                    table.CheckConstraint("CK_MedioPago_Campos", "([Tipo]=0 AND [BancoId] IS NOT NULL AND [NumeroCheque] IS NOT NULL AND [FechaVencimiento] IS NOT NULL AND [EstadoCheque] IS NOT NULL AND [EstadoCheque] IN(0,1,2,3,4,5) AND [FechaVencimiento]>=[Fecha] AND [CbuOrigen] IS NULL AND [CbuDestino] IS NULL AND [NumeroComprobante] IS NULL AND [Observaciones] IS NULL) OR ([Tipo]=1 AND [CbuOrigen] IS NOT NULL AND [CbuDestino] IS NOT NULL AND [NumeroComprobante] IS NOT NULL AND LEN(LTRIM(RTRIM([NumeroComprobante])))>0 AND [BancoId] IS NULL AND [NumeroCheque] IS NULL AND [FechaVencimiento] IS NULL AND [EstadoCheque] IS NULL AND [DetalleCheque] IS NULL) OR ([Tipo]=2 AND [BancoId] IS NULL AND [NumeroCheque] IS NULL AND [FechaVencimiento] IS NULL AND [EstadoCheque] IS NULL AND [DetalleCheque] IS NULL AND [CbuOrigen] IS NULL AND [CbuDestino] IS NULL AND [NumeroComprobante] IS NULL AND [Observaciones] IS NULL)");
                    table.CheckConstraint("CK_MedioPago_Importe", "[Importe]>0");
                    table.CheckConstraint("CK_MedioPago_Tipo", "[Tipo] IN (0,1,2)");
                    table.CheckConstraint("CK_Transferencia_Cbu", "[Tipo]<>1 OR ([CbuOrigen] IS NOT NULL AND [CbuDestino] IS NOT NULL AND DATALENGTH([CbuOrigen])=22 AND DATALENGTH([CbuDestino])=22 AND [CbuOrigen] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9]%' AND [CbuDestino] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9]%')");
                    table.ForeignKey(
                        name: "FK_MediosPagoProveedor_Bancos_BancoId",
                        column: x => x.BancoId,
                        principalTable: "Bancos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MediosPagoProveedor_OrdenesPagoProveedor_OrdenPagoProveedorId",
                        column: x => x.OrdenPagoProveedorId,
                        principalTable: "OrdenesPagoProveedor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrdenPagoProveedorFacturas",
                columns: table => new
                {
                    OrdenPagoProveedorId = table.Column<int>(type: "int", nullable: false),
                    FacturaProveedorId = table.Column<int>(type: "int", nullable: false),
                    ImporteTotalAlPagar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Vigente = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrdenPagoProveedorFacturas", x => new { x.OrdenPagoProveedorId, x.FacturaProveedorId });
                    table.CheckConstraint("CK_OPPFactura_Importe", "[ImporteTotalAlPagar]>0");
                    table.ForeignKey(
                        name: "FK_OrdenPagoProveedorFacturas_FacturasProveedor_FacturaProveedorId",
                        column: x => x.FacturaProveedorId,
                        principalTable: "FacturasProveedor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrdenPagoProveedorFacturas_OrdenesPagoProveedor_OrdenPagoProveedorId",
                        column: x => x.OrdenPagoProveedorId,
                        principalTable: "OrdenesPagoProveedor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResultadosOperacionPago",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    Operacion = table.Column<byte>(type: "tinyint", nullable: false),
                    Clave = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionCanonica = table.Column<int>(type: "int", nullable: false),
                    ContenidoCanonico = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Hash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    OrdenPagoProveedorId = table.Column<int>(type: "int", nullable: false),
                    EstadoHttp = table.Column<short>(type: "smallint", nullable: false),
                    ResultadoJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompletadaEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResultadosOperacionPago", x => x.Id);
                    table.CheckConstraint("CK_ResultadoPago_Json", "ISJSON([ContenidoCanonico])=1 AND ISJSON([ResultadoJson])=1");
                    table.CheckConstraint("CK_ResultadoPago_Operacion", "[Operacion] IN(0,1) AND [VersionCanonica]>0 AND [EstadoHttp] IN(200,201)");
                    table.ForeignKey(
                        name: "FK_ResultadosOperacionPago_OrdenesPagoProveedor_OrdenPagoProveedorId",
                        column: x => x.OrdenPagoProveedorId,
                        principalTable: "OrdenesPagoProveedor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResultadosOperacionPago_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HistorialEstadosCheque",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChequeId = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<byte>(type: "tinyint", nullable: false),
                    EstadoAnterior = table.Column<byte>(type: "tinyint", nullable: false),
                    EstadoNuevo = table.Column<byte>(type: "tinyint", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    OcurridoEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistorialEstadosCheque", x => x.Id);
                    table.CheckConstraint("CK_HistorialCheque_Arista", "([EstadoAnterior]=0 AND [EstadoNuevo] IN(1,5)) OR ([EstadoAnterior]=1 AND [EstadoNuevo] IN(2,5)) OR ([EstadoAnterior]=2 AND [EstadoNuevo] IN(3,4))");
                    table.CheckConstraint("CK_HistorialCheque_Tipo", "[Tipo]=0");
                    table.ForeignKey(
                        name: "FK_HistorialEstadosCheque_MediosPagoProveedor_ChequeId_Tipo",
                        columns: x => new { x.ChequeId, x.Tipo },
                        principalTable: "MediosPagoProveedor",
                        principalColumns: new[] { "Id", "Tipo" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistorialEstadosCheque_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OP_Origen_Liquidacion",
                table: "OrdenesDePago",
                columns: new[] { "OrdenPagoProveedorId", "LiquidacionId" },
                unique: true,
                filter: "[OrdenPagoProveedorId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OrdenesDePago_RevertidaPorId",
                table: "OrdenesDePago",
                column: "RevertidaPorId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrdenesDePago_Vigencia",
                table: "OrdenesDePago",
                sql: "([Vigente]=1 AND [RevertidaPorId] IS NULL AND [RevertidaEn] IS NULL) OR ([Vigente]=0 AND [OrdenPagoProveedorId] IS NOT NULL AND [RevertidaPorId] IS NOT NULL AND [RevertidaEn] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_Movimientos_OPP_Tipo",
                table: "MovimientosDeCaja",
                columns: new[] { "OrdenPagoProveedorId", "Tipo" },
                unique: true,
                filter: "[OrdenPagoProveedorId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MovimientosDeCaja_Referencia",
                table: "MovimientosDeCaja",
                sql: "([OrdenPagoProveedorId] IS NULL AND (([Tipo] = 0 AND [OrdenDePagoId] IS NULL) OR ([Tipo] = 1 AND [FacturaId] IS NULL))) OR ([OrdenPagoProveedorId] IS NOT NULL AND [Tipo] IN(0,1) AND [OrdenDePagoId] IS NULL AND [FacturaId] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FacturasProveedor_Estado",
                table: "FacturasProveedor",
                sql: "[Estado] IN (0,1)");

            migrationBuilder.CreateIndex(
                name: "IX_CambiosDeLiquidacion_Unica",
                table: "CambiosDeLiquidacion",
                columns: new[] { "LiquidacionId", "Operacion" },
                unique: true,
                filter: "[Operacion] IN (0,2,3)");

            migrationBuilder.CreateIndex(
                name: "IX_CambiosLiquidacion_OP_Operacion",
                table: "CambiosDeLiquidacion",
                columns: new[] { "OrdenDePagoId", "Operacion" },
                unique: true,
                filter: "[OrdenDePagoId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Bancos_Nombre",
                table: "Bancos",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistorialEstadosCheque_ChequeId_Id",
                table: "HistorialEstadosCheque",
                columns: new[] { "ChequeId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_HistorialEstadosCheque_ChequeId_Tipo",
                table: "HistorialEstadosCheque",
                columns: new[] { "ChequeId", "Tipo" });

            migrationBuilder.CreateIndex(
                name: "IX_HistorialEstadosCheque_UsuarioId",
                table: "HistorialEstadosCheque",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Cheques_Banco_Numero",
                table: "MediosPagoProveedor",
                columns: new[] { "BancoId", "NumeroCheque" },
                unique: true,
                filter: "[Tipo]=0");

            migrationBuilder.CreateIndex(
                name: "IX_MediosPagoProveedor_OrdenPagoProveedorId_RenglonId",
                table: "MediosPagoProveedor",
                columns: new[] { "OrdenPagoProveedorId", "RenglonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrdenesPagoProveedor_AnuladaPorId",
                table: "OrdenesPagoProveedor",
                column: "AnuladaPorId");

            migrationBuilder.CreateIndex(
                name: "IX_OrdenesPagoProveedor_Fecha_Id",
                table: "OrdenesPagoProveedor",
                columns: new[] { "Fecha", "Id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_OrdenesPagoProveedor_Numero",
                table: "OrdenesPagoProveedor",
                column: "Numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrdenesPagoProveedor_ProveedorId",
                table: "OrdenesPagoProveedor",
                column: "ProveedorId");

            migrationBuilder.CreateIndex(
                name: "IX_OrdenesPagoProveedor_UsuarioId",
                table: "OrdenesPagoProveedor",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_OPPFactura_Vigente",
                table: "OrdenPagoProveedorFacturas",
                column: "FacturaProveedorId",
                unique: true,
                filter: "[Vigente]=1");

            migrationBuilder.CreateIndex(
                name: "IX_ResultadoPago_Operacion_Clave",
                table: "ResultadosOperacionPago",
                columns: new[] { "Operacion", "Clave" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResultadosOperacionPago_OrdenPagoProveedorId",
                table: "ResultadosOperacionPago",
                column: "OrdenPagoProveedorId");

            migrationBuilder.CreateIndex(
                name: "IX_ResultadosOperacionPago_UsuarioId",
                table: "ResultadosOperacionPago",
                column: "UsuarioId");

            migrationBuilder.AddForeignKey(
                name: "FK_CambiosDeLiquidacion_OrdenesDePago_OrdenDePagoId",
                table: "CambiosDeLiquidacion",
                column: "OrdenDePagoId",
                principalTable: "OrdenesDePago",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MovimientosDeCaja_OrdenesPagoProveedor_OrdenPagoProveedorId",
                table: "MovimientosDeCaja",
                column: "OrdenPagoProveedorId",
                principalTable: "OrdenesPagoProveedor",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OrdenesDePago_OrdenesPagoProveedor_OrdenPagoProveedorId",
                table: "OrdenesDePago",
                column: "OrdenPagoProveedorId",
                principalTable: "OrdenesPagoProveedor",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OrdenesDePago_Usuarios_RevertidaPorId",
                table: "OrdenesDePago",
                column: "RevertidaPorId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // El downgrade técnico no puede borrar documentos ni padrón de negocio de 014.
            migrationBuilder.Sql("IF EXISTS(SELECT 1 FROM OrdenesPagoProveedor) OR EXISTS(SELECT 1 FROM Bancos) THROW 50014, N'No se puede revertir Gestión de pagos con datos registrados. Conservá la historia.', 1;");
            migrationBuilder.DropForeignKey(
                name: "FK_CambiosDeLiquidacion_OrdenesDePago_OrdenDePagoId",
                table: "CambiosDeLiquidacion");

            migrationBuilder.DropForeignKey(
                name: "FK_MovimientosDeCaja_OrdenesPagoProveedor_OrdenPagoProveedorId",
                table: "MovimientosDeCaja");

            migrationBuilder.DropForeignKey(
                name: "FK_OrdenesDePago_OrdenesPagoProveedor_OrdenPagoProveedorId",
                table: "OrdenesDePago");

            migrationBuilder.DropForeignKey(
                name: "FK_OrdenesDePago_Usuarios_RevertidaPorId",
                table: "OrdenesDePago");

            migrationBuilder.DropTable(
                name: "HistorialEstadosCheque");

            migrationBuilder.DropTable(
                name: "OrdenPagoProveedorFacturas");

            migrationBuilder.DropTable(
                name: "ResultadosOperacionPago");

            migrationBuilder.DropTable(
                name: "MediosPagoProveedor");

            migrationBuilder.DropTable(
                name: "Bancos");

            migrationBuilder.DropTable(
                name: "OrdenesPagoProveedor");

            migrationBuilder.DropIndex(
                name: "IX_OP_Origen_Liquidacion",
                table: "OrdenesDePago");

            migrationBuilder.DropIndex(
                name: "IX_OrdenesDePago_RevertidaPorId",
                table: "OrdenesDePago");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OrdenesDePago_Vigencia",
                table: "OrdenesDePago");

            migrationBuilder.DropIndex(
                name: "IX_Movimientos_OPP_Tipo",
                table: "MovimientosDeCaja");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MovimientosDeCaja_Referencia",
                table: "MovimientosDeCaja");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FacturasProveedor_Estado",
                table: "FacturasProveedor");

            migrationBuilder.DropIndex(
                name: "IX_CambiosDeLiquidacion_Unica",
                table: "CambiosDeLiquidacion");

            migrationBuilder.DropIndex(
                name: "IX_CambiosLiquidacion_OP_Operacion",
                table: "CambiosDeLiquidacion");

            migrationBuilder.DropColumn(
                name: "OrdenPagoProveedorId",
                table: "OrdenesDePago");

            migrationBuilder.DropColumn(
                name: "RevertidaEn",
                table: "OrdenesDePago");

            migrationBuilder.DropColumn(
                name: "RevertidaPorId",
                table: "OrdenesDePago");

            migrationBuilder.DropColumn(
                name: "Vigente",
                table: "OrdenesDePago");

            migrationBuilder.DropColumn(
                name: "OrdenPagoProveedorId",
                table: "MovimientosDeCaja");

            migrationBuilder.DropColumn(
                name: "OrdenDePagoId",
                table: "CambiosDeLiquidacion");

            migrationBuilder.DropSequence(
                name: "NumeroDeOrdenPagoProveedor");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MovimientosDeCaja_Referencia",
                table: "MovimientosDeCaja",
                sql: "([Tipo] = 0 AND [OrdenDePagoId] IS NULL) OR ([Tipo] = 1 AND [FacturaId] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FacturasProveedor_Estado",
                table: "FacturasProveedor",
                sql: "[Estado] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CambiosDeLiquidacion_Unica",
                table: "CambiosDeLiquidacion",
                columns: new[] { "LiquidacionId", "Operacion" },
                unique: true,
                filter: "[Operacion] <> 1");
        }
    }
}
