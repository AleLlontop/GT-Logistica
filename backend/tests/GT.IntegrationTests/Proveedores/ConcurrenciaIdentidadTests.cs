using System.Data.Common;
using GT.Application.Proveedores;
using GT.Application.Choferes.Transportistas;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Proveedores;

public class ConcurrenciaIdentidadTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task ContactoSinCambioDeCuitConservaLasReglasDelTransportistaNoVinculado()
    {
        await app.EnLaBaseAsync(async db =>
        {
            var cuit = PadronProveedorTests.Cuit(130);
            var externo = EscenarioProveedores.Externo(cuit);
            db.Transportistas.Add(externo); await db.SaveChangesAsync();
            // Un transportista sin proveedor Fletero todavía no es otra identidad de proveedor.
            await new RepositorioProveedores(db).GuardarAsync(null, new("proveedorNormal", "Normal", cuit), default);
            var resultado = await new ModificarTransportista(new RepositorioTransportistas(db)).EjecutarAsync(externo.Id,
                new("Contacto corregido", cuit, "fisica", "999", "nuevo@example.com"));
            Assert.True(resultado.Exitoso);
            Assert.Equal("999", resultado.Transportista!.Telefono);
        });
    }

    [Fact]
    public async Task DosAltasNormalizadasInclusoInactivoSoloConfirmanUna()
    {
        var exitos = 0;
        var duplicados = 0;
        async Task Alta(GtDbContext db, string cuit)
        {
            try { await new RepositorioProveedores(db).GuardarAsync(null, new("proveedorNormal", "Carrera", cuit), default); Interlocked.Increment(ref exitos); }
            catch (ProveedorException e) when (e.Codigo == "cuit_duplicado") { Interlocked.Increment(ref duplicados); }
        }
        await EscenarioProveedores.JuntasAsync(app, db => Alta(db, "30-71000000-6"), db => Alta(db, "30710000006"));
        Assert.Equal(1, exitos);
        Assert.Equal(1, duplicados);
        await app.EnLaBaseAsync(async db =>
        {
            var repo = new RepositorioProveedores(db);
            var proveedor = await db.Proveedores.SingleAsync(p => p.Cuit == "30710000006");
            await repo.CambiarEstadoAsync(proveedor.Id, proveedor.Version, false, default);
            await Assert.ThrowsAsync<ProveedorException>(() => repo.GuardarAsync(null, new("proveedorNormal", "Otro", "30-71000000-6"), default));
        });
    }

    [Fact]
    public async Task EdicionObsoletaNoSobrescribeNiReactiva()
    {
        int id = 0;
        await app.EnLaBaseAsync(async db => id = (await new RepositorioProveedores(db).GuardarAsync(null,
            new("proveedorNormal", "Original", PadronProveedorTests.Cuit(100)), default)).Id);
        var exitos = 0;
        var conflictos = 0;
        async Task Editar(GtDbContext db, string nombre)
        {
            try { await new RepositorioProveedores(db).GuardarAsync(id, new("proveedorNormal", nombre, PadronProveedorTests.Cuit(100), Version: 1), default); Interlocked.Increment(ref exitos); }
            catch (ProveedorException e) when (e.Tipo == TipoErrorProveedor.Conflicto) { Interlocked.Increment(ref conflictos); }
        }
        await EscenarioProveedores.JuntasAsync(app, db => Editar(db, "Primera"), db => Editar(db, "Segunda"));
        Assert.Equal(1, exitos);
        Assert.Equal(1, conflictos);
    }

    [Fact]
    public async Task FleteroYNormalCompitenPorIdentidadSinDuplicarla()
    {
        int externoId = 0;
        var cuit = PadronProveedorTests.Cuit(110);
        await app.EnLaBaseAsync(async db =>
        {
            if (!await db.EmpresaEmisora.AnyAsync()) db.EmpresaEmisora.Add(EscenarioProveedores.Emisor());
            var externo = EscenarioProveedores.Externo(cuit);
            db.Transportistas.Add(externo);
            await db.SaveChangesAsync(); externoId = externo.Id;
        });
        var exitos = 0;
        var duplicados = 0;
        async Task Alta(GtDbContext db, ProveedorPeticion peticion)
        {
            try { await new RepositorioProveedores(db).GuardarAsync(null, peticion, default); Interlocked.Increment(ref exitos); }
            catch (ProveedorException e) when (e.Codigo == "cuit_duplicado") { Interlocked.Increment(ref duplicados); }
        }
        await EscenarioProveedores.JuntasAsync(app, db => Alta(db, new("fletero", TransportistaId: externoId)),
            db => Alta(db, new("proveedorNormal", "Normal", cuit)));
        Assert.Equal(1, exitos);
        Assert.Equal(1, duplicados);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EdicionNormalOFleteroCompiteConAltaPorCuitNuevo(bool fletero)
    {
        int id = 0; int? transportistaId = null; long version = 0;
        var nuevoCuit = PadronProveedorTests.Cuit(fletero ? 143 : 141);
        await app.EnLaBaseAsync(async db =>
        {
            var repo = new RepositorioProveedores(db);
            ProveedorDetalle original;
            if (fletero)
            {
                if (!await db.EmpresaEmisora.AnyAsync()) db.EmpresaEmisora.Add(EscenarioProveedores.Emisor());
                var anterior = EscenarioProveedores.Externo(PadronProveedorTests.Cuit(142));
                db.Transportistas.Add(anterior); await db.SaveChangesAsync();
                original = await repo.GuardarAsync(null, new("fletero", TransportistaId: anterior.Id), default);
            }
            else original = await repo.GuardarAsync(null, new("proveedorNormal", "Original", PadronProveedorTests.Cuit(140)), default);
            id = original.Id; version = (await repo.CambiarEstadoAsync(id, original.Version, false, default)).Version;
            if (fletero)
            {
                if (!await db.EmpresaEmisora.AnyAsync()) db.EmpresaEmisora.Add(EscenarioProveedores.Emisor());
                var externo = EscenarioProveedores.Externo(nuevoCuit);
                db.Transportistas.Add(externo); await db.SaveChangesAsync(); transportistaId = externo.Id;
            }
        });
        var exitos = 0; var duplicados = 0;
        async Task Guardar(GtDbContext db, int? identificador, ProveedorPeticion peticion)
        {
            try { await new RepositorioProveedores(db).GuardarAsync(identificador, peticion, default); Interlocked.Increment(ref exitos); }
            catch (ProveedorException e) when (e.Codigo == "cuit_duplicado") { Interlocked.Increment(ref duplicados); }
        }
        var edicion = fletero ? new ProveedorPeticion("fletero", TransportistaId: transportistaId, Version: version)
            : new ProveedorPeticion("proveedorNormal", "Editado", nuevoCuit, Version: version);
        await EscenarioProveedores.JuntasAsync(app, db => Guardar(db, id, edicion),
            db => Guardar(db, null, new("proveedorNormal", "Alta concurrente", nuevoCuit.Insert(2, "-"))));
        Assert.Equal(1, exitos); Assert.Equal(1, duplicados);
        await app.EnLaBaseAsync(async db =>
        {
            Assert.False(await db.Proveedores.Where(p => p.Id == id).Select(p => p.Activo).SingleAsync());
            Assert.Equal(1, await db.Proveedores.CountAsync(p =>
                (p.Tipo == GT.Domain.Proveedores.TipoProveedor.Fletero ? p.Transportista!.Cuit : p.Cuit) == nuevoCuit));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TransportistaCoordinaAunSinVinculoContraCuitAntesAusente(bool vinculado)
    {
        int id = 0;
        var propuesto = PadronProveedorTests.Cuit(vinculado ? 121 : 123);
        await app.EnLaBaseAsync(async db =>
        {
            if (!await db.EmpresaEmisora.AnyAsync()) db.EmpresaEmisora.Add(EscenarioProveedores.Emisor());
            var externo = EscenarioProveedores.Externo(PadronProveedorTests.Cuit(vinculado ? 120 : 122));
            db.Transportistas.Add(externo); await db.SaveChangesAsync(); id = externo.Id;
            if (vinculado) await new RepositorioProveedores(db).GuardarAsync(null, new("fletero", TransportistaId: id), default);
        });
        var alta = false;
        var modificacion = false;
        await EscenarioProveedores.JuntasAsync(app, async db =>
        {
            try { await new RepositorioProveedores(db).GuardarAsync(null, new("proveedorNormal", "Nuevo", propuesto), default); alta = true; }
            catch (ProveedorException e) when (e.Codigo == "cuit_duplicado") { }
        }, async db =>
        {
            var resultado = await new ModificarTransportista(new RepositorioTransportistas(db)).EjecutarAsync(id,
                new("Externo", propuesto.Insert(2, "-"), "fisica", "341", "a@b.com"));
            modificacion = resultado.Exitoso;
        });
        // Sin Fletero un transportista puede coexistir con normal: no agrega una identidad de proveedor.
        // Si alta fue primero, la modificación se rechaza; si modificación fue primero, alta sigue válida.
        Assert.True(alta || modificacion);
        if (vinculado) Assert.NotEqual(alta, modificacion);
    }

    [Fact]
    public async Task CoordinadorEsperaAntesDeResolverSinFilasYOrdenaAnteriorYPropuesto()
    {
        using var alcance = app.Services.CreateScope();
        var baseDb = alcance.ServiceProvider.GetRequiredService<GtDbContext>();
        var captura = new CapturaLocks();
        var opciones = new DbContextOptionsBuilder<GtDbContext>().UseSqlServer(baseDb.Database.GetConnectionString()).AddInterceptors(captura).Options;
        await using var primero = new GtDbContext(opciones);
        await using var segundo = new GtDbContext(opciones);
        await using var tx1 = await primero.Database.BeginTransactionAsync();
        await new CoordinadorIdentidadProveedor(primero).AdquirirAsync(() => Task.FromResult<IEnumerable<string>>(["30-71234567-1", "20123456786", "20-12345678-6"]));
        Assert.Equal(new[] { "GT.Proveedores.Identidad", "GT.Proveedores.Cuit:20123456786", "GT.Proveedores.Cuit:30712345671" }, captura.Recursos);
        await using var tx2 = await segundo.Database.BeginTransactionAsync();
        var resuelto = false;
        var esperando = new CoordinadorIdentidadProveedor(segundo).AdquirirAsync(() => { resuelto = true; return Task.FromResult<IEnumerable<string>>(["20123456786"]); });
        Assert.False(resuelto);
        Assert.False(esperando.IsCompleted);
        await tx1.RollbackAsync();
        await esperando.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(resuelto);
        await tx2.RollbackAsync();
    }

    private sealed class CapturaLocks : DbCommandInterceptor
    {
        public List<string> Recursos { get; } = [];
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("sp_getapplock")) Recursos.Add((string)command.Parameters[0].Value!);
            return ValueTask.FromResult(result);
        }
    }
}
