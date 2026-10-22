using System.Data.Common;
using System.Net;
using System.Text;
using System.Text.Json;
using GT.Api.Pagos;
using GT.Application.Pagos;
using GT.Domain.Pagos;
using GT.Domain.Usuarios;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Facturacion;
using GT.IntegrationTests.Infraestructura;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Pagos;

public class IdempotenciaGenerarTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    private static readonly DateOnly Hoy = new(2026, 10, 18);
    private static GenerarOrdenEntrada Entrada(int proveedor, int factura, int banco) => new(Guid.NewGuid(), proveedor, Hoy,
        null, [factura], [new ChequeEntrada(Guid.NewGuid(), 121m, banco, "000-123", Hoy, Hoy, null)], 121m, true);
    private static async Task<ResultadoEscrituraPago> Ejecutar(AplicacionDePrueba aplicacion, GenerarOrdenEntrada entrada,
        int usuario, bool consulta = true, DateOnly? hoy = null)
    {
        using var scope = aplicacion.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IRepositorioPagos>().GenerarAsync(entrada, usuario, consulta,
            hoy ?? Hoy, new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task ReplayGlobalEntreUsuarios_AutorizaAntesDeComparar_YConservaResultadoTrasOtroDia()
    {
        var s = await EscenarioPagos.CrearAsync(app);
        var otro = await app.CrearUsuarioConPermisosAsync("replay_" + Guid.NewGuid().ToString("N")[..12], "Pagos.12345678", CodigosPermiso.PagosGenerar);
        var e = Entrada(s.Orden.ProveedorId, s.Factura.Id, s.Banco.Id);
        var original = await Ejecutar(app, e, s.UsuarioId);
        Assert.Equal(original, await Ejecutar(app, e, s.UsuarioId, hoy: Hoy.AddDays(1)));
        Assert.Equal(403, (await Assert.ThrowsAsync<ReglaPagoException>(() => Ejecutar(app, e, otro.Id, false))).EstadoHttp);
        Assert.Equal(403, (await Assert.ThrowsAsync<ReglaPagoException>(() => Ejecutar(app, e with { Detalle = "Distinto" }, otro.Id, false))).EstadoHttp);
        Assert.Equal(original, await Ejecutar(app, e, otro.Id, true));
        Assert.Equal(409, (await Assert.ThrowsAsync<ReglaPagoException>(() => Ejecutar(app, e with { Detalle = "Distinto" }, otro.Id))).EstadoHttp);
        Assert.Equal(409, (await Assert.ThrowsAsync<ReglaPagoException>(() => Ejecutar(app, e with { ClaveOperacion = Guid.NewGuid() }, s.UsuarioId))).EstadoHttp);
        await app.EnLaBaseAsync(async db =>
        {
            var resultado = Assert.Single(await db.ResultadosOperacionPago.Where(x => x.Clave == e.ClaveOperacion).ToListAsync());
            Assert.Equal(s.UsuarioId, resultado.UsuarioId);
            Assert.Single(await db.OrdenPagoProveedorFacturas.Where(x => x.FacturaProveedorId == s.Factura.Id).ToListAsync());
        });
    }

    [Fact]
    public async Task DosConfirmacionesMismaClave_SeEncuentranAntesDelApplockYDevuelvenMismoResultado()
    {
        var prueba = new AplicacionIntervenida();
        try
        {
            await prueba.InitializeAsync();
            var s = await EscenarioPagos.CrearAsync(prueba);
            var e = Entrada(s.Orden.ProveedorId, s.Factura.Id, s.Banco.Id);
            prueba.Comandos.BarreraActiva = true;
            var respuestas = await Task.WhenAll(Ejecutar(prueba, e, s.UsuarioId), Ejecutar(prueba, e, s.UsuarioId)).WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Equal(2, prueba.Comandos.Llegadas);
            Assert.Equal(respuestas[0], respuestas[1]);
            await prueba.EnLaBaseAsync(async db => Assert.Single(await db.ResultadosOperacionPago.Where(x => x.Clave == e.ClaveOperacion).ToListAsync()));
        }
        finally { await prueba.DisposeAsync(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommitRealConRespuestaPerdida_RecuperaONoDetermina_SiempreMismaClave(bool fallaRecuperacion)
    {
        var prueba = new AplicacionIntervenida();
        try
        {
            await prueba.InitializeAsync();
            var s = await EscenarioPagos.CrearAsync(prueba);
            var e = Entrada(s.Orden.ProveedorId, s.Factura.Id, s.Banco.Id);
            prueba.Commit.Activo = true;
            prueba.Comandos.FallarRecuperacion = fallaRecuperacion;
            if (fallaRecuperacion)
            {
                var error = await Assert.ThrowsAsync<ReglaPagoException>(() => Ejecutar(prueba, e, s.UsuarioId));
                Assert.Equal(503, error.EstadoHttp);
                Assert.Equal("resultado_no_determinable", error.Codigo);
            }
            else Assert.Equal(201, (await Ejecutar(prueba, e, s.UsuarioId)).EstadoHttp);
            Assert.True(prueba.Commit.Disparado);
            prueba.Comandos.FallarRecuperacion = false;
            var replay = await Ejecutar(prueba, e, s.UsuarioId);
            Assert.Equal(201, replay.EstadoHttp);
            await prueba.EnLaBaseAsync(async db =>
            {
                Assert.Single(await db.ResultadosOperacionPago.Where(x => x.Clave == e.ClaveOperacion).ToListAsync());
                Assert.Single(await db.OrdenPagoProveedorFacturas.Where(x => x.FacturaProveedorId == s.Factura.Id).ToListAsync());
                Assert.Single(await db.Cheques.Where(x => x.BancoId == s.Banco.Id).ToListAsync());
            });
        }
        finally { await prueba.DisposeAsync(); }
    }

    [Fact]
    public async Task PermisoRevocado_ImpideReplayAunqueLaClaveEsteConfirmada()
    {
        var s = await EscenarioPagos.CrearAsync(app);
        var nombre = "rev_" + Guid.NewGuid().ToString("N")[..12];
        var usuario = await app.CrearUsuarioConPermisosAsync(nombre, "Pagos.12345678", CodigosPermiso.PagosGenerar);
        using var cliente = await app.CrearClienteAutenticadoAsync(nombre, "Pagos.12345678");
        var e = Entrada(s.Orden.ProveedorId, s.Factura.Id, s.Banco.Id);
        Task<HttpResponseMessage> Post() => cliente.PostAsync("/api/pagos/ordenes", new StringContent(JsonSerializer.Serialize(e, SerializadorPagos.Opciones), Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Created, (await Post()).StatusCode);
        await app.EnLaBaseAsync(async db =>
        {
            var u = await db.Usuarios.Include(x => x.Roles).SingleAsync(x => x.Id == usuario.Id);
            u.Roles.Clear();
            await db.SaveChangesAsync();
        });
        Assert.Equal(HttpStatusCode.Forbidden, (await Post()).StatusCode);
    }

    internal sealed class AplicacionIntervenida : AplicacionDePrueba
    {
        public CommitPerdido Commit { get; } = new();
        public ComandosIntervenidos Comandos { get; }
        public AplicacionIntervenida() => Comandos = new(Commit);
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(s => s.AddDbContext<GtDbContext>(o => o.AddInterceptors(Commit, Comandos)));
        }
    }
    internal sealed class CommitPerdido : DbTransactionInterceptor
    {
        public bool Activo;
        public bool Disparado;
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            if (Activo && !Disparado)
            {
                Disparado = true;
                throw new IOException("Respuesta del commit perdida después del commit SQL real.");
            }
            return Task.CompletedTask;
        }
    }
    internal sealed class ComandosIntervenidos(CommitPerdido commit) : DbCommandInterceptor
    {
        public bool BarreraActiva;
        public bool FallarRecuperacion;
        private int llegadas;
        public int Llegadas => llegadas;
        private readonly TaskCompletionSource salida = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("sp_getapplock", StringComparison.Ordinal))
            {
                if (FallarRecuperacion && commit.Disparado) throw new IOException("Lectura de recuperación no disponible.");
                if (BarreraActiva)
                {
                    var numero = Interlocked.Increment(ref llegadas);
                    if (numero == 2) salida.TrySetResult();
                    if (numero <= 2) await salida.Task.WaitAsync(cancellationToken);
                }
            }
            return result;
        }
    }
}
