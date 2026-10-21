using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Net;
using System.Text.Json;
using GT.Domain.Facturacion;
using GT.Domain.Usuarios;
using GT.Domain.Viajes;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Caja;
using GT.IntegrationTests.Facturacion;
using GT.IntegrationTests.Flota;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Liquidaciones;
using GT.IntegrationTests.Pagos;
using GT.IntegrationTests.Viajes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Reportes;

public class DashboardAnalisisTests(AplicacionObservadaAnalisis app) : IClassFixture<AplicacionObservadaAnalisis>
{
    private const string Ruta = "/api/reportes/dashboard?desde=2026-10-01&hasta=2026-10-31";

    [Fact]
    public async Task BaseAisladaNoConsultaSecciones_YFuenteSolaNoEntra()
    {
        foreach(var modo in new[]{"base","fuente","ambos"})
        {
            var nombre="dash_"+Guid.NewGuid().ToString("N")[..10];
            var permisos = modo switch
            {
                "base" => new[]{CodigosPermiso.ReportesConsultar},
                "fuente" => new[]{CodigosPermiso.ViajesConsultar},
                _ => new[]{CodigosPermiso.ReportesConsultar,CodigosPermiso.ViajesConsultar},
            };
            await app.CrearUsuarioConPermisosAsync(nombre,"Reportes.12345678",permisos);
            using var http=await app.CrearClienteAutenticadoAsync(nombre,"Reportes.12345678");
            app.Sonda.Iniciar();
            using var respuesta=await http.GetAsync(Ruta);
            app.Sonda.Activa=false;
            Assert.Equal(modo=="fuente"?HttpStatusCode.Forbidden:HttpStatusCode.OK,respuesta.StatusCode);
            if(modo!="ambos") Assert.DoesNotContain(app.Sonda.Comandos,c=>EsNegocio(c.Sql));
            else Assert.DoesNotContain(app.Sonda.Comandos,c=>EsNegocio(c.Sql) && !c.Sql.Contains("[Viajes]",StringComparison.Ordinal));
            if(modo=="fuente")continue;
            using var json=JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
            foreach(var seccion in json.RootElement.GetProperty("secciones").EnumerateObject())
            {
                var disponible=modo=="ambos" && seccion.Name=="viajes";
                Assert.Equal(disponible?"disponible":"sinPermiso",seccion.Value.GetProperty("estado").GetString());
                Assert.Equal(disponible?JsonValueKind.Object:JsonValueKind.Null,seccion.Value.GetProperty("datos").ValueKind);
            }
            if(modo=="ambos")
            {
                using var vacio=await http.GetAsync("/api/reportes/dashboard?desde=1900-01-01&hasta=1900-01-31");
                using var datos=JsonDocument.Parse(await vacio.Content.ReadAsStringAsync());
                var viajes=datos.RootElement.GetProperty("secciones").GetProperty("viajes");
                Assert.Equal("disponible",viajes.GetProperty("estado").GetString());
                Assert.Equal(0,viajes.GetProperty("datos").GetProperty("resumen").GetProperty("cantidad").GetInt64());
            }
        }
    }

    [Fact]
    public async Task KpiYSeriesSeparanDocumentosYFlujos_UnaFotoSnapshot_SinConsultarDetalle()
    {
        var cliente=await app.CrearClienteAsync();
        await app.CrearFacturaAsync(cliente.Id,fecha:new(2026,9,15),estado:EstadoFactura.Pagada,fechaCobro:new(2026,10,10),neto:100m);
        await app.CrearFacturaAsync(cliente.Id,fecha:new(2026,10,18),neto:200m);
        var propietario=await app.TransportistaExternoAsync();
        var viajeAnterior=await app.ViajeDeAsync(cliente.Id,propietario.Id,importe:100m,fecha:new(2026,9,10));
        await app.CrearLiquidacionAsync(propietario.Id,[viajeAnterior],importePagado:50m,mes:9);
        await EscenarioAnulacion.CrearAsync(app,"fletero");
        var tipo=await app.CrearTipoVehiculoAsync();
        var vehiculo=await app.CrearVehiculoAsync(tipo.Id,propietario.Id);
        await app.CrearViajeAsync(cliente.Id,new(2026,10,18),EstadoViaje.Rendido,vehiculoId:vehiculo.Id);
        var usuario=await app.UsuarioAsync();
        var caja=await app.CrearCajaAsync(usuario.Id);
        await app.CrearMovimientoAsync(caja.Id,usuario.Id,tipo:GT.Domain.Caja.TipoMovimientoCaja.Egreso,importe:5m,fecha:new(2026,10,18,12,0,0,DateTimeKind.Utc));
        using var http=await app.CrearClienteAutenticadoAsync();
        app.Sonda.Iniciar();
        using var respuesta=await http.GetAsync(Ruta);
        app.Sonda.Activa=false;
        Assert.Equal(HttpStatusCode.OK,respuesta.StatusCode);
        using var json=JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        var secciones=json.RootElement.GetProperty("secciones");
        Assert.All(secciones.EnumerateObject(),s=>Assert.Equal("disponible",s.Value.GetProperty("estado").GetString()));
        var ventas=secciones.GetProperty("facturacion").GetProperty("datos");
        Assert.Equal("242.00",ventas.GetProperty("documentos").GetProperty("facturado").GetString());
        Assert.Equal("242.00",ventas.GetProperty("documentos").GetProperty("pendienteVigenteDocumental").GetString());
        Assert.Equal("121.00",ventas.GetProperty("cobros").GetProperty("importeCobrado").GetString());
        var lq=secciones.GetProperty("liquidaciones").GetProperty("datos");
        Assert.Equal("363.00",lq.GetProperty("documentos").GetProperty("importeLiquidado").GetString());
        Assert.Equal("413.00",lq.GetProperty("pagos").GetProperty("importePagadoVigente").GetString());
        Assert.Equal("-5.00",secciones.GetProperty("caja").GetProperty("datos").GetProperty("resumen").GetProperty("neto").GetString());
        Assert.Equal(1,secciones.GetProperty("flota").GetProperty("datos").GetProperty("resumen").GetProperty("totalViajesConVehiculo").GetInt64());
        Assert.Equal(json.RootElement.GetProperty("contexto").GetProperty("hoyArgentina").GetString(),secciones.GetProperty("vencimientosFacturas").GetProperty("datos").GetProperty("contexto").GetProperty("hoyArgentina").GetString());
        var negocio=app.Sonda.Comandos.Where(c=>EsNegocio(c.Sql)).ToArray();
        Assert.NotEmpty(negocio);
        Assert.All(negocio,c=>Assert.Equal(IsolationLevel.Snapshot,c.Aislamiento));
        Assert.DoesNotContain(negocio,c=>c.Sql.Contains("OFFSET",StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("\"detalle\"",json.RootElement.GetRawText());
    }

    [Fact]
    public async Task FalloSqlNoDevuelveFotoParcialNiCeros()
    {
        using var http=await app.CrearClienteAutenticadoAsync();
        app.Sonda.Iniciar(); app.Sonda.FalloTabla="[MovimientosDeCaja]";
        using var respuesta=await http.GetAsync(Ruta);
        app.Sonda.Activa=false;
        Assert.Equal(HttpStatusCode.InternalServerError,respuesta.StatusCode);
        using var json=JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        Assert.Equal("analisis_no_generado",json.RootElement.GetProperty("codigo").GetString());
        Assert.False(json.RootElement.TryGetProperty("secciones",out _));
        Assert.DoesNotContain("50015",json.RootElement.GetRawText());
    }

    private static bool EsNegocio(string sql) => new[]{"[Viajes]","[Facturas]","[Liquidaciones]","[OrdenesDePago]","[MovimientosDeCaja]","[Vehiculos]","[Documentaciones]","[DocumentacionesVehiculo]"}.Any(sql.Contains);
}

public class AplicacionObservadaAnalisis : AplicacionAnalisisDePrueba
{
    public SondaConsultasAnalisis Sonda { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(s=>s.AddDbContext<GtDbContext>(o=>o.AddInterceptors(Sonda)));
    }
}

public sealed class SondaConsultasAnalisis : DbCommandInterceptor
{
    public bool Activa;
    public string? FalloTabla;
    public ConcurrentQueue<(string Sql,IsolationLevel? Aislamiento)> Comandos { get; } = new();
    public void Iniciar() { Comandos.Clear(); FalloTabla=null; Activa=true; }
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,CommandEventData eventData,InterceptionResult<DbDataReader> result,CancellationToken cancellationToken=default)
    {
        if(Activa)
        {
            Comandos.Enqueue((command.CommandText,eventData.Context?.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel));
            if(FalloTabla is { } tabla && command.CommandText.Contains(tabla,StringComparison.Ordinal))
            { FalloTabla=null; command.CommandText="THROW 50015, 'Fallo sintético de análisis', 1;"; }
        }
        return ValueTask.FromResult(result);
    }
}
