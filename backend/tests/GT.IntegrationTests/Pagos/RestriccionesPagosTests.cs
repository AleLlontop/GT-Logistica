using GT.Domain.Pagos;
using GT.IntegrationTests.Infraestructura;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Pagos;

public class RestriccionesPagosTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Theory]
    [InlineData(" ")]
    [InlineData("  Banco ")]
    [InlineData("Banco ")]
    [InlineData(" Banco")]
    [InlineData("\tBanco\u00a0")]
    public async Task Banco_InsertSinTrimEsRechazadoPorCheck(string valor)
    {
        await app.EnLaBaseAsync(async db =>
        {
            var error = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT Bancos(Nombre,Activo,Version) VALUES({valor},1,0)"));
            Assert.Contains("CK_Bancos_Nombre", error.Message);
        });
    }

    [Fact]
    public async Task Banco_NormalizadoConEspacioInternoAdmitido_YDuplicadoInclusoInactivoRechazado()
    {
        var nombre = "Banco  Mixto " + Guid.NewGuid().ToString("N");
        await app.EnLaBaseAsync(async db =>
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT Bancos(Nombre,Activo,Version) VALUES({nombre},0,0)");
            Assert.Equal(nombre, await db.Bancos.Where(x => x.Nombre == nombre).Select(x => x.Nombre).SingleAsync());
            var error = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT Bancos(Nombre,Activo,Version) VALUES({nombre},1,0)"));
            Assert.Contains("IX_Bancos_Nombre", error.Message);
            // SQL decide igualdad de nombres según su collation heredada; la preconsulta será la misma.
            var equivalente = await db.Bancos.AnyAsync(x => x.Nombre == nombre.ToUpperInvariant());
            if (equivalente)
                await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT Bancos(Nombre,Activo,Version) VALUES({nombre.ToUpperInvariant()},1,0)"));
        });
    }

    [Fact]
    public async Task Banco_200Admitido_201NoSeTrunca()
    {
        await app.EnLaBaseAsync(async db =>
        {
            var nombre = new string('A', 168) + Guid.NewGuid().ToString("N");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT Bancos(Nombre,Activo,Version) VALUES({nombre},1,0)");
            Assert.Equal(200, (await db.Bancos.SingleAsync(x => x.Nombre == nombre)).Nombre.Length);
            await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT Bancos(Nombre,Activo,Version) VALUES({nombre + "X"},1,0)"));
        });
    }

    private Task InsertarCheque(int opp, int banco, string? numero) => app.EnLaBaseAsync(db =>
        db.Database.ExecuteSqlInterpolatedAsync($@"INSERT MediosPagoProveedor
            (OrdenPagoProveedorId,RenglonId,Tipo,Importe,Fecha,BancoId,NumeroCheque,FechaVencimiento,EstadoCheque)
            VALUES({opp},{Guid.NewGuid()},0,121,'20261018',{banco},{numero},'20261019',0)"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" 000123")]
    [InlineData("000123 ")]
    [InlineData("\u3000000123\t")]
    public async Task Cheque_InsertSinNormalizarOVacioSeRechaza(string? numero)
    {
        var s = await EscenarioPagos.CrearAsync(app);
        await Assert.ThrowsAsync<SqlException>(() => InsertarCheque(s.Orden.Id, s.Banco.Id, numero));
    }

    [Fact]
    public async Task Cheque_IdentidadBinariaPreservaCerosSeparadoresEspaciosYLetras()
    {
        var s = await EscenarioPagos.CrearAsync(app);
        string[] numeros = ["000123", "123", "001-123", "001123", "Ab/  001", "ab/  001", "Ab/ 001"];
        foreach (var n in numeros) await InsertarCheque(s.Orden.Id, s.Banco.Id, n);
        var duplicado = await Assert.ThrowsAsync<SqlException>(() => InsertarCheque(s.Orden.Id, s.Banco.Id, "000123"));
        Assert.Contains("IX_Cheques_Banco_Numero", duplicado.Message);
        var otro = await EscenarioPagos.CrearAsync(app);
        await InsertarCheque(s.Orden.Id, otro.Banco.Id, "000123");
        await app.EnLaBaseAsync(async db =>
        {
            var leidos = await db.Cheques.Where(x => x.BancoId == s.Banco.Id).Select(x => x.NumeroCheque).ToListAsync();
            Assert.Equal(numeros.Order(), leidos.Order());
            await db.Cheques.Where(x => x.BancoId == s.Banco.Id && x.NumeroCheque == "000123")
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.EstadoCheque, EstadoCheque.Anulado));
        });
        await Assert.ThrowsAsync<SqlException>(() => InsertarCheque(s.Orden.Id, s.Banco.Id, "000123"));
    }

    [Fact]
    public async Task Cheque_200Admitido_201Rechazado_SinTruncado()
    {
        var s = await EscenarioPagos.CrearAsync(app);
        await InsertarCheque(s.Orden.Id, s.Banco.Id, new string('A', 200));
        await Assert.ThrowsAsync<SqlException>(() => InsertarCheque(s.Orden.Id, s.Banco.Id, new string('B', 201)));
    }

    [Fact]
    public async Task TrimUnicode_ParidadSqlYAplicacion_EnAmbosCampos()
    {
        var s = await EscenarioPagos.CrearAsync(app);
        foreach (var blanco in Enumerable.Range(0, char.MaxValue + 1).Select(i => (char)i).Where(char.IsWhiteSpace))
        {
            var valor = blanco + "Prueba" + blanco;
            Assert.Equal("Prueba", ReglasDePagos.NormalizarNumeroCheque(valor));
            await Assert.ThrowsAsync<SqlException>(() => InsertarCheque(s.Orden.Id, s.Banco.Id, valor));
            await app.EnLaBaseAsync(async db =>
            {
                var error = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT Bancos(Nombre,Activo,Version) VALUES({valor},1,0)"));
                Assert.Contains("CK_Bancos_Nombre", error.Message);
            });
        }
    }

    [Fact]
    public async Task Vínculo_UnaVigenteEHistoriaAnuladaConservada_YFalseRealmenteSeInserta()
    {
        var s = await EscenarioPagos.CrearAsync(app);
        await app.EnLaBaseAsync(async db =>
        {
            for (var i = 0; i < 4; i++)
            {
                var otra = new OrdenPagoProveedor { ProveedorId = s.Orden.ProveedorId, ProveedorRazonSocial = s.Orden.ProveedorRazonSocial,
                    ProveedorCuit = s.Orden.ProveedorCuit, Fecha = s.Orden.Fecha, UsuarioId = s.UsuarioId,
                    ImporteTotal = 121m, RegistradaEn = s.Orden.RegistradaEn };
                db.OrdenesPagoProveedor.Add(otra);
                await db.SaveChangesAsync();
                db.OrdenPagoProveedorFacturas.Add(new() { OrdenPagoProveedorId = otra.Id, FacturaProveedorId = s.Factura.Id,
                    Vigente = false, ImporteTotalAlPagar = 121m });
                await db.SaveChangesAsync();
            }
            Assert.Equal(4, await db.OrdenPagoProveedorFacturas.CountAsync(x => x.FacturaProveedorId == s.Factura.Id && !x.Vigente));
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT OrdenPagoProveedorFacturas VALUES({s.Orden.Id},{s.Factura.Id},121,1)");
            var nueva = new OrdenPagoProveedor { ProveedorId = s.Orden.ProveedorId,
                ProveedorRazonSocial = s.Orden.ProveedorRazonSocial, ProveedorCuit = s.Orden.ProveedorCuit,
                Fecha = s.Orden.Fecha, UsuarioId = s.UsuarioId, ImporteTotal = 121m, RegistradaEn = s.Orden.RegistradaEn };
            db.Add(nueva);
            await db.SaveChangesAsync();
            var error = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT OrdenPagoProveedorFacturas VALUES({nueva.Id},{s.Factura.Id},121,1)"));
            Assert.Contains("IX_OPPFactura_Vigente", error.Message);
        });
    }

    [Fact]
    public async Task Tph_CamposCruzadosImporteYCbuInvalidosSeRechazan()
    {
        var s = await EscenarioPagos.CrearAsync(app);
        await app.EnLaBaseAsync(async db =>
        {
            var cruzado = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($@"INSERT MediosPagoProveedor
                (OrdenPagoProveedorId,RenglonId,Tipo,Importe,Fecha,BancoId,CbuOrigen,CbuDestino,NumeroComprobante)
                VALUES({s.Orden.Id},{Guid.NewGuid()},1,121,'20261018',{s.Banco.Id},'0000000000000000000000','0000000000000000000001','1')"));
            Assert.Contains("CK_MedioPago_Campos", cruzado.Message);
            var importe = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($@"INSERT MediosPagoProveedor
                (OrdenPagoProveedorId,RenglonId,Tipo,Importe,Fecha) VALUES({s.Orden.Id},{Guid.NewGuid()},2,0,'20261018')"));
            Assert.Contains("CK_MedioPago_Importe", importe.Message);
            var cbu = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($@"INSERT MediosPagoProveedor
                (OrdenPagoProveedorId,RenglonId,Tipo,Importe,Fecha,CbuOrigen,CbuDestino,NumeroComprobante)
                VALUES({s.Orden.Id},{Guid.NewGuid()},1,121,'20261018','A000000000000000000000','0000000000000000000001','1')"));
            Assert.Contains("CK_Transferencia_Cbu", cbu.Message);
        });
    }

    [Fact]
    public async Task Historial_SoloChequeYAristasPermitidas()
    {
        var s = await EscenarioPagos.CrearAsync(app);
        await InsertarCheque(s.Orden.Id, s.Banco.Id, "1");
        await app.EnLaBaseAsync(async db =>
        {
            var cheque = await db.Cheques.SingleAsync(x => x.BancoId == s.Banco.Id);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT HistorialEstadosCheque(ChequeId,Tipo,EstadoAnterior,EstadoNuevo,UsuarioId,OcurridoEn) VALUES({cheque.Id},0,0,1,{s.UsuarioId},{s.Orden.RegistradaEn})");
            var arista = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT HistorialEstadosCheque(ChequeId,Tipo,EstadoAnterior,EstadoNuevo,UsuarioId,OcurridoEn) VALUES({cheque.Id},0,0,3,{s.UsuarioId},{s.Orden.RegistradaEn})"));
            Assert.Contains("CK_HistorialCheque_Arista", arista.Message);
            var efectivo = new Efectivo { OrdenPagoProveedorId = s.Orden.Id, RenglonId = Guid.NewGuid(), Importe = 1m, Fecha = s.Orden.Fecha };
            db.MediosPagoProveedor.Add(efectivo);
            await db.SaveChangesAsync();
            var fk = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT HistorialEstadosCheque(ChequeId,Tipo,EstadoAnterior,EstadoNuevo,UsuarioId,OcurridoEn) VALUES({efectivo.Id},0,0,1,{s.UsuarioId},{s.Orden.RegistradaEn})"));
            Assert.Equal(547, fk.Number);
        });
    }

    [Fact]
    public async Task Resultado_ClaveGlobalPorOperacion_YNoPorUsuario()
    {
        var s = await EscenarioPagos.CrearAsync(app);
        var clave = Guid.NewGuid();
        await app.EnLaBaseAsync(async db =>
        {
            var fila = new ResultadoOperacionPago { UsuarioId = s.UsuarioId, Operacion = TipoOperacionPago.Generar, Clave = clave,
                ContenidoCanonico = "{}", Hash = new byte[32], OrdenPagoProveedorId = s.Orden.Id,
                EstadoHttp = 201, ResultadoJson = "{}", CompletadaEn = s.Orden.RegistradaEn };
            db.ResultadosOperacionPago.Add(fila);
            await db.SaveChangesAsync();
            var error = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($@"INSERT ResultadosOperacionPago
                (UsuarioId,Operacion,Clave,VersionCanonica,ContenidoCanonico,Hash,OrdenPagoProveedorId,EstadoHttp,ResultadoJson,CompletadaEn)
                VALUES({s.UsuarioId},0,{clave},1,'{{}}',{new byte[32]},{s.Orden.Id},201,'{{}}',{s.Orden.RegistradaEn})"));
            Assert.Contains("IX_ResultadoPago_Operacion_Clave", error.Message);
        });
    }
    [Fact]
    public async Task OpManual_NoReversibleSinOrigen_YOrigenConservadoTrasReversion()
    {
        var s = await EscenarioPagos.CrearAsync(app);
        await app.EnLaBaseAsync(async db =>
        {
            var transportista = GT.IntegrationTests.Proveedores.EscenarioProveedores.Externo(
                "20" + Random.Shared.NextInt64(100000000, 999999999));
            var liquidacion = GT.IntegrationTests.Proveedores.EscenarioProveedores.Pendiente(transportista, 121m);
            liquidacion.ImportePagado = 30m;
            var op = new GT.Domain.Liquidaciones.OrdenDePago { LiquidacionId = 0, Liquidacion = liquidacion,
                FechaPago = s.Orden.Fecha, Importe = 30m, UsuarioId = s.UsuarioId, RegistradaEn = s.Orden.RegistradaEn };
            db.Add(op);
            await db.SaveChangesAsync();
            var error = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE OrdenesDePago SET Vigente=0, RevertidaPorId={s.UsuarioId}, RevertidaEn={s.Orden.RegistradaEn} WHERE Id={op.Id}"));
            Assert.Contains("CK_OrdenesDePago_Vigencia", error.Message);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE OrdenesDePago SET OrdenPagoProveedorId={s.Orden.Id}, Vigente=0, RevertidaPorId={s.UsuarioId}, RevertidaEn={s.Orden.RegistradaEn} WHERE Id={op.Id}");
            db.ChangeTracker.Clear();
            var historia = await db.OrdenesDePago.AsNoTracking().SingleAsync(x => x.Id == op.Id);
            Assert.False(historia.Vigente);
            Assert.Equal(s.Orden.Id, historia.OrdenPagoProveedorId);
            Assert.Equal(op.Numero, historia.Numero);
            Assert.Equal(30m, historia.Importe);
        });
    }

    [Fact]
    public async Task Caja_UnEgresoYUnIngresoConFkOppIndependiente_SinBorrarOriginal()
    {
        var s = await EscenarioPagos.CrearAsync(app);
        await app.EnLaBaseAsync(async db =>
        {
            var caja = new GT.Domain.Caja.Caja { UsuarioResponsableId = s.UsuarioId, SaldoInicial = 121m,
                FechaApertura = s.Orden.RegistradaEn };
            db.Add(caja);
            await db.SaveChangesAsync();
            foreach (var tipo in new[] { GT.Domain.Caja.TipoMovimientoCaja.Egreso, GT.Domain.Caja.TipoMovimientoCaja.Ingreso })
                await db.Database.ExecuteSqlInterpolatedAsync($@"INSERT MovimientosDeCaja
                    (CajaId,Tipo,Importe,Concepto,UsuarioId,Fecha,OrdenPagoProveedorId)
                    VALUES({caja.Id},{(byte)tipo},121,'OPP de prueba',{s.UsuarioId},{s.Orden.RegistradaEn},{s.Orden.Id})");
            var error = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($@"INSERT MovimientosDeCaja
                (CajaId,Tipo,Importe,Concepto,UsuarioId,Fecha,OrdenPagoProveedorId)
                VALUES({caja.Id},1,121,'duplicado',{s.UsuarioId},{s.Orden.RegistradaEn},{s.Orden.Id})"));
            Assert.Contains("IX_Movimientos_OPP_Tipo", error.Message);
            var movimientos = await db.MovimientosDeCaja.Where(x => x.OrdenPagoProveedorId == s.Orden.Id).ToListAsync();
            Assert.Equal(2, movimientos.Count);
            Assert.All(movimientos, x => Assert.Null(x.OrdenDePagoId));
            Assert.All(movimientos, x => Assert.Null(x.FacturaId));
            // FK equivocada: un Id inexistente en OPP nunca se resuelve contra otra tabla.
            await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($@"INSERT MovimientosDeCaja
                (CajaId,Tipo,Importe,Concepto,UsuarioId,Fecha,OrdenPagoProveedorId)
                VALUES({caja.Id},1,121,'referencia inválida',{s.UsuarioId},{s.Orden.RegistradaEn},2147483647)"));
        });
    }

    [Fact]
    public async Task Opp_AnuladaSinAuditoriaNoPasaCheckNiConCamposNulos()
    {
        var s = await EscenarioPagos.CrearAsync(app);
        await app.EnLaBaseAsync(async db =>
        {
            var error = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE OrdenesPagoProveedor SET Estado=1 WHERE Id={s.Orden.Id}"));
            Assert.Contains("CK_OPP_Estado", error.Message);
        });
    }

}
