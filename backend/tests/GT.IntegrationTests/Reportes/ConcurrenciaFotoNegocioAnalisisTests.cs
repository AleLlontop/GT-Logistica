using System.Data.Common;
using System.Net;
using System.Text.Json;
using GT.Application.Facturacion;
using GT.Application.Liquidaciones;
using GT.Application.Pagos;
using GT.Domain.Viajes;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Facturacion;
using GT.IntegrationTests.Flota;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Liquidaciones;
using GT.IntegrationTests.Pagos;
using GT.IntegrationTests.Viajes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Reportes;

public class ConcurrenciaFotoNegocioAnalisisTests
{
    [Theory]
    [InlineData("cobro",false)] [InlineData("cobro",true)]
    [InlineData("pago",false)] [InlineData("pago",true)]
    [InlineData("anulacion",false)] [InlineData("anulacion",true)]
    [InlineData("tipo",false)] [InlineData("tipo",true)]
    public async Task CommitEntreSelect_RespuestaCoherente_RequestSiguienteActualizado(string caso,bool dashboard)
    {
        var app=new AplicacionConBarreraFoto();
        try
        {
            await app.InitializeAsync();
            var cliente=await app.CrearClienteAsync();
            var administrador=await app.ConAlcanceAsync(DatosDePruebaLiquidaciones.AdministradorAsync);
            Func<Task> escribir;
            string recurso,tabla;
            int tipoAntes=0,tipoDespues=0;
            if(caso=="cobro")
            {
                var factura=await app.CrearFacturaAsync(cliente.Id,fecha:new(2026,9,15),neto:100m);
                recurso="facturacion/cobros"; tabla="[Facturas]";
                escribir=async()=>
                {
                    using var scope=app.Services.CreateScope();
                    var resultado=await scope.ServiceProvider.GetRequiredService<RegistrarCobro>().EjecutarAsync(factura.Id,new(new DateOnly(2026,10,18)),administrador.Id);
                    Assert.True(resultado.Exitoso);
                };
            }
            else if(caso=="pago")
            {
                var propietario=await app.TransportistaExternoAsync();
                var viaje=await app.ViajeDeAsync(cliente.Id,propietario.Id,importe:100m,fecha:new(2026,9,10));
                var liquidacion=await app.CrearLiquidacionAsync(propietario.Id,[viaje],mes:9,generadaEn:new(2026,9,12,12,0,0,DateTimeKind.Utc));
                recurso="liquidaciones/pagos"; tabla=dashboard?"[Liquidaciones]":"[OrdenesDePago]";
                escribir=async()=>
                {
                    using var scope=app.Services.CreateScope();
                    Assert.NotNull(await scope.ServiceProvider.GetRequiredService<IRepositorioLiquidaciones>().RegistrarPagoAsync(liquidacion.Id,new(2026,10,18),50m,administrador.Id,new(2026,10,18,12,0,0,DateTimeKind.Utc)));
                };
            }
            else if(caso=="anulacion")
            {
                var pago=await EscenarioAnulacion.CrearAsync(app,"fletero");
                recurso="liquidaciones/pagos"; tabla=dashboard?"[Liquidaciones]":"[OrdenesDePago]";
                escribir=async()=>
                {
                    using var scope=app.Services.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<IRepositorioPagos>().AnularAsync(pago.OrdenId,new(Guid.NewGuid(),"Reversión concurrente",true),pago.UsuarioId,false,new(2026,11,1),new(2026,11,1,12,0,0,DateTimeKind.Utc));
                };
            }
            else
            {
                var propietario=await app.TransportistaExternoAsync();
                var tipo=await app.CrearTipoVehiculoAsync();var nuevo=await app.CrearTipoVehiculoAsync();
                tipoAntes=tipo.Id;tipoDespues=nuevo.Id;
                var vehiculo=await app.CrearVehiculoAsync(tipo.Id,propietario.Id);
                await app.CrearViajeAsync(cliente.Id,new(2026,10,18),EstadoViaje.Rendido,vehiculoId:vehiculo.Id);
                recurso="flota";tabla="[Vehiculos]";
                escribir=()=>app.EnLaBaseAsync(db=>db.Vehiculos.Where(v=>v.Id==vehiculo.Id).ExecuteUpdateAsync(s=>s.SetProperty(v=>v.TipoVehiculoId,nuevo.Id)));
            }
            using var http=await app.CrearClienteAutenticadoAsync();
            var ruta="/api/reportes/"+(dashboard?"dashboard":recurso)+"?desde=2026-10-01&hasta=2026-10-31";
            using var limite=new CancellationTokenSource(TimeSpan.FromSeconds(30));
            app.Barrera.Armar(tabla);
            var pendiente=http.GetAsync(ruta,limite.Token);
            await app.Barrera.Primera.Task.WaitAsync(limite.Token);
            try { await escribir().WaitAsync(limite.Token); }
            finally { app.Barrera.Liberada.TrySetResult(); }
            using var respuesta=await pendiente;
            Assert.Equal(HttpStatusCode.OK,respuesta.StatusCode);
            using var antes=JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
            using var siguiente=await http.GetAsync(ruta,limite.Token);
            Assert.Equal(HttpStatusCode.OK,siguiente.StatusCode);
            using var despues=JsonDocument.Parse(await siguiente.Content.ReadAsStringAsync());
            if(caso=="tipo")
            {
                static string Tipo(JsonElement raiz,bool dash)=>dash
                    ?raiz.GetProperty("secciones").GetProperty("flota").GetProperty("datos").GetProperty("tipo").GetProperty("top")[0].GetProperty("id").GetString()!
                    :raiz.GetProperty("detalle").GetProperty("items")[0].GetProperty("tipo").GetProperty("id").GetInt32().ToString();
                Assert.Equal(tipoAntes.ToString(),Tipo(antes.RootElement,dashboard));
                Assert.Equal(tipoDespues.ToString(),Tipo(despues.RootElement,dashboard));
                if(!dashboard)
                    Assert.Equal(tipoAntes.ToString(),antes.RootElement.GetProperty("distribuciones").GetProperty("tipo").GetProperty("top")[0].GetProperty("id").GetString());
            }
            else
            {
                JsonElement Resumen(JsonElement raiz)=>!dashboard?raiz.GetProperty("resumen"):
                    caso=="cobro"?raiz.GetProperty("secciones").GetProperty("facturacion").GetProperty("datos").GetProperty("cobros"):
                    raiz.GetProperty("secciones").GetProperty("liquidaciones").GetProperty("datos").GetProperty("pagos");
                var campo=caso=="cobro"?"importeCobrado":"importePagadoVigente";
                Assert.Equal(caso=="anulacion"?"363.00":"0.00",Resumen(antes.RootElement).GetProperty(campo).GetString());
                Assert.Equal(caso=="cobro"?"121.00":caso=="pago"?"50.00":"0.00",Resumen(despues.RootElement).GetProperty(campo).GetString());
                if(!dashboard)
                {
                    var filas=antes.RootElement.GetProperty("detalle").GetProperty("items");
                    Assert.Equal(caso=="anulacion"?3:0,filas.GetArrayLength());
                    if(caso=="anulacion")Assert.All(filas.EnumerateArray(),f=>Assert.True(f.GetProperty("vigente").GetBoolean()));
                }
                if(dashboard && caso=="anulacion")
                {
                    Assert.Equal("363.00",antes.RootElement.GetProperty("secciones").GetProperty("liquidaciones").GetProperty("datos").GetProperty("documentos").GetProperty("pagadoVigenteDocumental").GetString());
                    Assert.Equal("0.00",despues.RootElement.GetProperty("secciones").GetProperty("liquidaciones").GetProperty("datos").GetProperty("documentos").GetProperty("pagadoVigenteDocumental").GetString());
                }
            }
        }
        finally { app.Barrera.Liberada.TrySetResult(); await app.DisposeAsync(); }
    }
}

public class AplicacionConBarreraFoto : AplicacionAnalisisDePrueba
{
    public BarreraLecturaAnalisis Barrera { get; }=new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(s=>s.AddDbContext<GtDbContext>(o=>o.AddInterceptors(Barrera)));
    }
}

public sealed class BarreraLecturaAnalisis : DbCommandInterceptor
{
    private int armada;
    private string tabla="";
    public TaskCompletionSource Primera { get; private set; }=new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Liberada { get; private set; }=new(TaskCreationOptions.RunContinuationsAsynchronously);
    public void Armar(string objetivo) { tabla=objetivo;Primera=new(TaskCreationOptions.RunContinuationsAsynchronously);Liberada=new(TaskCreationOptions.RunContinuationsAsynchronously);Interlocked.Exchange(ref armada,1); }
    public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command,CommandExecutedEventData eventData,DbDataReader result,CancellationToken cancellationToken=default)
    {
        if(command.CommandText.Contains(tabla,StringComparison.Ordinal) && Interlocked.CompareExchange(ref armada,0,1)==1)
        {
            Primera.TrySetResult();
            await Liberada.Task.WaitAsync(cancellationToken);
        }
        return result;
    }
}
