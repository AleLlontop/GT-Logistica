using GT.Application.Choferes;
using GT.Application.Reportes.Analisis;
using GT.Application.Viajes;
using GT.Application.Viajes.Clientes;
using GT.Domain.Flota;
using GT.Domain.Viajes;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Choferes;
using GT.IntegrationTests.Flota;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Viajes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Reportes;

public class BusquedaTextoReportesTests(AplicacionAnalisisDePrueba app) : IClassFixture<AplicacionAnalisisDePrueba>
{
    private static readonly string[] Formas = ["logística", "logistica", "LOGÍSTICA", "LOGISTICA"];
    private static readonly PeriodoAnalisis Periodo = new(new(2026, 10, 1), new(2026, 10, 31));
    private static readonly DateTimeOffset Instante = new(2026, 11, 1, 0, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task ClientesYReferencias_BuscanAmbasGrafiasAntesDePaginar_SinConfundirEnie()
    {
        var prefijo = $"ZZZClientes{ClaveUnica()}";
        var clientes = new List<Cliente>();
        for (var i = 0; i < 22; i++)
            clientes.Add(await app.CrearClienteAsync(razonSocial: $"{prefijo} {(i % 2 == 0 ? "Logística" : "Logistica")} {i:D2} {prefijo}"));
        var ene = await app.CrearClienteAsync(razonSocial: $"{prefijo} Pena");
        var enie = await app.CrearClienteAsync(razonSocial: $"{prefijo} Peña");

        using var scope = app.Services.CreateScope();
        var padron = scope.ServiceProvider.GetRequiredService<IRepositorioClientes>();
        var viajes = scope.ServiceProvider.GetRequiredService<IConsultaViajesAnalisis>();
        var facturacion = scope.ServiceProvider.GetRequiredService<IConsultaFacturacionAnalisis>();
        var esperados = clientes.Select(c => new ReferenciaAnalisis(c.Id, c.RazonSocial, c.Activo)).ToArray();
        async Task<PaginaDe<ReferenciaAnalisis>> ConsultarPadron(string? texto, FiltroPaginaAnalisis pagina)
        {
            var resultado = await padron.ConsultarAsync(new(Busqueda: texto, Pagina: pagina.Pagina));
            return new(resultado.Items.Select(c => new ReferenciaAnalisis(c.Id, c.RazonSocial, c.Activo)).ToArray(),
                resultado.Total, resultado.Pagina, resultado.TamanioPagina);
        }

        await VerificarReferenciasAsync(ConsultarPadron, prefijo, esperados, ene.Id, enie.Id);
        await VerificarReferenciasAsync((texto, pagina) => viajes.ReferenciasViajesAsync(false, texto, pagina),
            prefijo, esperados, ene.Id, enie.Id);
        await VerificarReferenciasAsync((texto, pagina) => facturacion.ReferenciasClientesFacturacionAsync(texto, pagina),
            prefijo, esperados, ene.Id, enie.Id);

        var ids = clientes.Select(c => c.Id).Append(ene.Id).Append(enie.Id).ToArray();
        var persistidos = await scope.ServiceProvider.GetRequiredService<GtDbContext>().Clientes.AsNoTracking()
            .Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.RazonSocial);
        Assert.All(clientes.Append(ene).Append(enie), c => Assert.Equal(c.RazonSocial, persistidos[c.Id]));
    }

    [Fact]
    public async Task ReferenciasTransportistas_CompartenBusquedaEnViajesLiquidacionesYFlota()
    {
        var prefijo = $"ZZZTransportistas{ClaveUnica()}";
        var esperados = new List<ReferenciaAnalisis>();
        for (var i = 0; i < 22; i++)
        {
            var transportista = await app.CrearTransportistaAsync(nombre: $"{prefijo} {(i % 2 == 0 ? "Logística" : "Logistica")} {i:D2} {prefijo}");
            esperados.Add(new(transportista.Id, transportista.Nombre, transportista.Activo));
        }
        var ene = await app.CrearTransportistaAsync(nombre: $"{prefijo} Pena");
        var enie = await app.CrearTransportistaAsync(nombre: $"{prefijo} Peña");

        using var scope = app.Services.CreateScope();
        var viajes = scope.ServiceProvider.GetRequiredService<IConsultaViajesAnalisis>();
        var liquidaciones = scope.ServiceProvider.GetRequiredService<IConsultaLiquidacionesAnalisis>();
        var flota = scope.ServiceProvider.GetRequiredService<IConsultaFlotaAnalisis>();
        await VerificarReferenciasAsync((texto, pagina) => viajes.ReferenciasViajesAsync(true, texto, pagina),
            prefijo, esperados, ene.Id, enie.Id);
        await VerificarReferenciasAsync((texto, pagina) => liquidaciones.ReferenciasTransportistasLiquidacionesAsync(texto, pagina),
            prefijo, esperados, ene.Id, enie.Id);
        await VerificarReferenciasAsync((texto, pagina) => flota.ReferenciasFlotaAsync(ReferenciaDeAnalisis.TransportistasFlota, texto, pagina),
            prefijo, esperados, ene.Id, enie.Id);

        var ids = esperados.Select(r => r.Id).Append(ene.Id).Append(enie.Id).ToArray();
        var persistidos = await scope.ServiceProvider.GetRequiredService<GtDbContext>().Transportistas.AsNoTracking()
            .Where(t => ids.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Nombre);
        Assert.All(esperados, t => Assert.Equal(t.Nombre, persistidos[t.Id]));
        Assert.Equal(ene.Nombre, persistidos[ene.Id]);
        Assert.Equal(enie.Nombre, persistidos[enie.Id]);
    }

    [Fact]
    public async Task ReferenciasTipos_BuscanSinAcentosConOrdenYTotal_ConservanEnieYNombres()
    {
        var prefijo = $"ZZZTipos{ClaveUnica()}";
        var tipos = new List<TipoVehiculo>();
        for (var i = 0; i < 22; i++)
            tipos.Add(await app.CrearTipoVehiculoAsync(nombre: $"{prefijo} {(i % 2 == 0 ? "Logística" : "Logistica")} {i:D2} {prefijo}"));
        var ene = await app.CrearTipoVehiculoAsync(nombre: $"{prefijo} Pena");
        var enie = await app.CrearTipoVehiculoAsync(nombre: $"{prefijo} Peña");

        using var scope = app.Services.CreateScope();
        var flota = scope.ServiceProvider.GetRequiredService<IConsultaFlotaAnalisis>();
        await VerificarReferenciasAsync((texto, pagina) => flota.ReferenciasFlotaAsync(ReferenciaDeAnalisis.TiposVehiculo, texto, pagina),
            prefijo, tipos.Select(t => new ReferenciaAnalisis(t.Id, t.Nombre, t.Activo)).ToArray(), ene.Id, enie.Id);

        var ids = tipos.Select(t => t.Id).Append(ene.Id).Append(enie.Id).ToArray();
        var persistidos = await scope.ServiceProvider.GetRequiredService<GtDbContext>().TiposVehiculo.AsNoTracking()
            .Where(t => ids.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Nombre);
        Assert.All(tipos.Append(ene).Append(enie), t => Assert.Equal(t.Nombre, persistidos[t.Id]));
    }

    [Theory]
    [InlineData("origen")]
    [InlineData("destino")]
    [InlineData("cliente")]
    public async Task Viajes_ListadoAnalisisYExportacion_BuscanCadaCampoAntesDePaginar(string campo)
    {
        var prefijo = $"Viajes{ClaveUnica()}";
        var comun = await app.CrearClienteAsync();
        var filas = new List<(Viaje Viaje, Cliente Cliente)>();
        async Task<(Viaje Viaje, Cliente Cliente)> CrearAsync(string texto)
        {
            var cliente = campo == "cliente" ? await app.CrearClienteAsync(razonSocial: texto) : comun;
            var viaje = await app.CrearViajeAsync(cliente.Id, new(2026, 10, 18), EstadoViaje.Rendido,
                origen: campo == "origen" ? texto : "Rosario", destino: campo == "destino" ? texto : "Salta", importe: 1.01m);
            return (viaje, cliente);
        }
        for (var i = 0; i < 22; i++)
            filas.Add(await CrearAsync($"{prefijo} {(i % 2 == 0 ? "Logistica" : "Logística")} {i:D2} {prefijo}"));
        var ene = await CrearAsync($"{prefijo} Pena");
        var enie = await CrearAsync($"{prefijo} Peña");

        using var scope = app.Services.CreateScope();
        var listado = scope.ServiceProvider.GetRequiredService<IRepositorioViajes>();
        var analisis = scope.ServiceProvider.GetRequiredService<IConsultaViajesAnalisis>();
        var contexto = ContextoAnalisis.DesdeInstante(Instante, Periodo, CriterioTemporalAnalisis.FechaViaje);
        var momento = new MomentoDeLectura(new(2026, 10, 31), Instante.UtcDateTime);
        var esperados = filas.AsEnumerable().Reverse().ToArray();
        var sinFiltro = await listado.ConsultarAsync(new(Desde: Periodo.Desde, Hasta: Periodo.Hasta), momento);
        Assert.DoesNotContain(sinFiltro.Items, v => v.Id == filas[0].Viaje.Id);

        foreach (var forma in Formas)
        {
            var texto = $"{prefijo} {forma}";
            var filtro = new FiltroViajesAnalisis(Periodo, Busqueda: texto);
            var primera = await analisis.ViajesAsync(filtro, contexto);
            var segunda = await analisis.ViajesAsync(filtro with { Pagina = 2 }, contexto);
            Assert.Equal(22, primera.Detalle.Total);
            Assert.Equal(22, segunda.Detalle.Total);
            Assert.Equal(20, primera.Detalle.Items.Count);
            Assert.Equal(2, segunda.Detalle.Items.Count);
            Assert.Equal(esperados.Select(f => f.Viaje.Id), primera.Detalle.Items.Concat(segunda.Detalle.Items).Select(v => v.Id));
            Assert.Equal(22, primera.Resumen.Cantidad);
            Assert.Equal(22.22m, primera.Resumen.ImporteTotal);
            Assert.Equal(primera.Resumen, segunda.Resumen);

            foreach (var pagina in new[] { 1, 2 })
            {
                var resultado = await listado.ConsultarAsync(new(Desde: Periodo.Desde, Hasta: Periodo.Hasta, Busqueda: texto, Pagina: pagina), momento);
                Assert.Equal(22, resultado.Total);
                Assert.Equal(esperados.Skip((pagina - 1) * 20).Take(20).Select(f => f.Viaje.Id), resultado.Items.Select(v => v.Id));
            }
        }

        // El término acentuado parcial encuentra también el texto guardado sin acento, fuera de la primera página original.
        var parcial = await analisis.ViajesAsync(new(Periodo, Busqueda: $"ÍSTICA 00 {prefijo}"), contexto);
        Assert.Equal(filas[0].Viaje.Id, Assert.Single(parcial.Detalle.Items).Id);
        foreach (var (texto, esperado) in new[] { ($"{prefijo} PENA", ene), ($"{prefijo} PEÑA", enie) })
        {
            var resultado = await analisis.ViajesAsync(new(Periodo, Busqueda: texto), contexto);
            Assert.Equal(1, resultado.Detalle.Total);
            Assert.Equal(esperado.Viaje.Id, Assert.Single(resultado.Detalle.Items).Id);
            var resultadoListado = await listado.ConsultarAsync(new(Desde: Periodo.Desde, Hasta: Periodo.Hasta, Busqueda: texto), momento);
            Assert.Equal(1, resultadoListado.Total);
            Assert.Equal(esperado.Viaje.Id, Assert.Single(resultadoListado.Items).Id);
        }

        var exportado = await scope.ServiceProvider.GetRequiredService<IExportacionAnalisis>()
            .ExportarViajesAsync(new(Periodo, Busqueda: $"{prefijo} LOGÍSTICA", Pagina: 2), Instante, "gerencia");
        Assert.Equal(22, exportado.Encabezado.CantidadDeFilas);
        Assert.Equal(esperados.Select(f => f.Viaje.Numero), exportado.Filas.Select(f => f[0].ValorEntero!.Value));
        for (var i = 0; i < esperados.Length; i++)
        {
            Assert.Equal(esperados[i].Cliente.RazonSocial, exportado.Filas[i][2].ValorTexto);
            Assert.Equal(esperados[i].Viaje.Origen, exportado.Filas[i][3].ValorTexto);
            Assert.Equal(esperados[i].Viaje.Destino, exportado.Filas[i][4].ValorTexto);
        }

        var todos = filas.Append(ene).Append(enie).ToArray();
        var ids = todos.Select(f => f.Viaje.Id).ToArray();
        var persistidos = await scope.ServiceProvider.GetRequiredService<GtDbContext>().Viajes.AsNoTracking()
            .Where(v => ids.Contains(v.Id)).Select(v => new { v.Id, v.Origen, v.Destino, v.Cliente!.RazonSocial })
            .ToDictionaryAsync(v => v.Id);
        Assert.All(todos, f =>
        {
            Assert.Equal(f.Viaje.Origen, persistidos[f.Viaje.Id].Origen);
            Assert.Equal(f.Viaje.Destino, persistidos[f.Viaje.Id].Destino);
            Assert.Equal(f.Cliente.RazonSocial, persistidos[f.Viaje.Id].RazonSocial);
        });
    }

    [Fact]
    public async Task Comodines_ConservanLikeEnClientesYViajes_YSonLiteralesEnReferencias()
    {
        var prefijo = $"Especial{ClaveUnica()}";
        var literal = await app.CrearClienteAsync(razonSocial: $"{prefijo}%Carga");
        var otro = await app.CrearClienteAsync(razonSocial: $"{prefijo}OtraCarga");
        var viajeLiteral = await app.CrearViajeAsync(literal.Id, new(2026, 10, 18), EstadoViaje.Rendido);
        var viajeOtro = await app.CrearViajeAsync(otro.Id, new(2026, 10, 18), EstadoViaje.Rendido);
        var tipoLiteral = await app.CrearTipoVehiculoAsync(nombre: $"{prefijo}%Carga");
        await app.CrearTipoVehiculoAsync(nombre: $"{prefijo}OtraCarga");
        using var scope = app.Services.CreateScope();
        var texto = $"{prefijo}%";
        var clientes = await scope.ServiceProvider.GetRequiredService<IRepositorioClientes>().ConsultarAsync(new(Busqueda: texto));
        Assert.Equal(2, clientes.Total);
        Assert.Equal(new[] { literal.Id, otro.Id }.Order(), clientes.Items.Select(c => c.Id).Order());
        var viajes = scope.ServiceProvider.GetRequiredService<IConsultaViajesAnalisis>();
        var contexto = ContextoAnalisis.DesdeInstante(Instante, Periodo, CriterioTemporalAnalisis.FechaViaje);
        var resultado = await viajes.ViajesAsync(new(Periodo, Busqueda: texto), contexto);
        Assert.Equal(2, resultado.Detalle.Total);
        Assert.Equal(new[] { viajeOtro.Id, viajeLiteral.Id }, resultado.Detalle.Items.Select(v => v.Id));
        var referencias = await viajes.ReferenciasViajesAsync(false, texto, new());
        Assert.Equal(1, referencias.Total);
        Assert.Equal(literal.Id, Assert.Single(referencias.Items).Id);
        var tipos = await scope.ServiceProvider.GetRequiredService<IConsultaFlotaAnalisis>()
            .ReferenciasFlotaAsync(ReferenciaDeAnalisis.TiposVehiculo, texto, new());
        Assert.Equal(1, tipos.Total);
        Assert.Equal(tipoLiteral.Id, Assert.Single(tipos.Items).Id);
    }

    private static string ClaveUnica() => Guid.NewGuid().ToString("N")[..12];

    private static async Task VerificarReferenciasAsync(
        Func<string?, FiltroPaginaAnalisis, Task<PaginaDe<ReferenciaAnalisis>>> consultar,
        string prefijo, IReadOnlyList<ReferenciaAnalisis> esperados, int eneId, int enieId)
    {
        Assert.DoesNotContain((await consultar(null, new())).Items, r => r.Id == esperados[^1].Id);
        foreach (var forma in Formas)
        {
            var primera = await consultar($"{prefijo} {forma}", new());
            var segunda = await consultar($"{prefijo} {forma}", new(2));
            Assert.Equal(22, primera.Total);
            Assert.Equal(22, segunda.Total);
            Assert.Equal(20, primera.Items.Count);
            Assert.Equal(2, segunda.Items.Count);
            var todos = primera.Items.Concat(segunda.Items).ToArray();
            Assert.Equal(esperados.Select(r => r.Id), todos.Select(r => r.Id));
            Assert.Equal(esperados.Select(r => r.Nombre), todos.Select(r => r.Nombre));
        }

        var parcial = await consultar($"ÍSTICA 21 {prefijo}", new());
        Assert.Equal(1, parcial.Total);
        Assert.Equal(esperados[^1].Id, Assert.Single(parcial.Items).Id);
        foreach (var (texto, id) in new[] { ($"{prefijo} PENA", eneId), ($"{prefijo} PEÑA", enieId) })
        {
            var resultado = await consultar(texto, new());
            Assert.Equal(1, resultado.Total);
            Assert.Equal(id, Assert.Single(resultado.Items).Id);
        }
    }
}
