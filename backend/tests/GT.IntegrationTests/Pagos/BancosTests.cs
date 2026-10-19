using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using GT.Application.Pagos;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Infraestructura;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Pagos;

public class BancosTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Theory]
    [InlineData(200, HttpStatusCode.Created)]
    [InlineData(201, HttpStatusCode.BadRequest)]
    public async Task LimiteSeEvaluaTrasTrimSinTruncar(int longitud, HttpStatusCode esperado)
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var nombre = new string('A', longitud - 32) + Guid.NewGuid().ToString("N");
        var respuesta = await cliente.PostAsJsonAsync("/api/pagos/bancos", new { nombre = "  " + nombre + "  " });
        Assert.Equal(esperado, respuesta.StatusCode);
        if (esperado == HttpStatusCode.Created) Assert.Equal(nombre, (await respuesta.Content.ReadFromJsonAsync<BancoDto>())!.Nombre);
    }

    [Fact]
    public async Task InactivoSigueOcupandoNombreYChequeHistoricoSobreviveRenombre()
    {
        var s = await EscenarioPagos.CrearAsync(app);
        await app.EnLaBaseAsync(async db =>
        {
            db.Cheques.Add(new() { OrdenPagoProveedorId = s.Orden.Id, BancoId = s.Banco.Id, NumeroCheque = "000123",
                RenglonId = Guid.NewGuid(), Fecha = s.Orden.Fecha, FechaVencimiento = s.Orden.Fecha.AddDays(1), Importe = 121m });
            await db.SaveChangesAsync();
        });
        var cliente = await app.CrearClienteAutenticadoAsync();
        await cliente.PutAsJsonAsync($"/api/pagos/bancos/{s.Banco.Id}/desactivacion", new { });
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PostAsJsonAsync("/api/pagos/bancos", new { nombre = s.Banco.Nombre })).StatusCode);
        var nombre = "Banco histórico " + Guid.NewGuid().ToString("N");
        var editar = await cliente.PutAsJsonAsync($"/api/pagos/bancos/{s.Banco.Id}", new { nombre, version = s.Banco.Version });
        Assert.Equal(HttpStatusCode.OK, editar.StatusCode);
        await app.EnLaBaseAsync(async db =>
        {
            var cheque = await db.Cheques.Include(x => x.Banco).SingleAsync(x => x.BancoId == s.Banco.Id);
            Assert.Equal(nombre, cheque.Banco!.Nombre);
            Assert.False(cheque.Banco.Activo);
            Assert.Equal(s.Orden.Id, cheque.OrdenPagoProveedorId);
        });
    }

    [Theory]
    [InlineData("alta")]
    [InlineData("nombre")]
    [InlineData("version")]
    public async Task EscriturasConcurrentesRespetanUnicidadYVersion(string caso)
    {
        var prueba = new AplicacionConBarrera();
        try
        {
            await prueba.InitializeAsync();
            var a = await prueba.CrearClienteAutenticadoAsync();
            var b = await prueba.CrearClienteAutenticadoAsync();
            BancoDto? bancoA = null, bancoB = null;
            if (caso != "alta")
            {
                bancoA = (await (await a.PostAsJsonAsync("/api/pagos/bancos", new { nombre = "Original A" })).Content.ReadFromJsonAsync<BancoDto>())!;
                bancoB = caso == "version" ? bancoA : (await (await a.PostAsJsonAsync("/api/pagos/bancos", new { nombre = "Original B" })).Content.ReadFromJsonAsync<BancoDto>())!;
            }
            prueba.Barrera.Habilitar();
            using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var primera = caso == "alta"
                ? a.PostAsJsonAsync("/api/pagos/bancos", new { nombre = "Concurrente" }, limite.Token)
                : a.PutAsJsonAsync($"/api/pagos/bancos/{bancoA!.Id}", new { nombre = "Concurrente", version = bancoA.Version }, limite.Token);
            var segunda = caso == "alta"
                ? b.PostAsJsonAsync("/api/pagos/bancos", new { nombre = "Concurrente" }, limite.Token)
                : b.PutAsJsonAsync($"/api/pagos/bancos/{bancoB!.Id}", new { nombre = caso == "version" ? "Otro nombre" : "Concurrente", version = bancoB.Version }, limite.Token);
            var respuestas = await Task.WhenAll(primera, segunda);
            Assert.Equal(2, prueba.Barrera.CantidadBloqueada);
            Assert.Equal(1, respuestas.Count(x => x.StatusCode == (caso == "alta" ? HttpStatusCode.Created : HttpStatusCode.OK)));
            Assert.Equal(1, respuestas.Count(x => x.StatusCode == (caso == "version" ? HttpStatusCode.Conflict : HttpStatusCode.BadRequest)));
            await prueba.EnLaBaseAsync(async db => Assert.Equal(1, await db.Bancos.CountAsync(x => x.Nombre == "Concurrente" || caso == "version" && x.Nombre == "Otro nombre")));
        }
        finally { await prueba.DisposeAsync(); }
    }

    private class AplicacionConBarrera : AplicacionDePrueba
    {
        public BarreraNombre Barrera { get; } = new();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services => services.AddDbContext<GtDbContext>(o => o.AddInterceptors(Barrera)));
        }
    }
    private class BarreraNombre : DbCommandInterceptor
    {
        private bool habilitada;
        private int llegadas;
        private readonly TaskCompletionSource salida = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int CantidadBloqueada => Math.Min(2, llegadas);
        public void Habilitar() => habilitada = true;
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (habilitada && command.CommandText.Contains("FROM [Bancos]", StringComparison.Ordinal)
                && command.CommandText.Contains("[Nombre] =", StringComparison.Ordinal))
            {
                var numero = Interlocked.Increment(ref llegadas);
                if (numero <= 2)
                {
                    if (numero == 2) salida.TrySetResult();
                    await salida.Task.WaitAsync(cancellationToken);
                }
            }
            return result;
        }
    }
}
