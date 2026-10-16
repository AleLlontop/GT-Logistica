using GT.Application.Proveedores;
using GT.Domain.Proveedores;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;

namespace GT.IntegrationTests.Proveedores;

public class PadronProveedorTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task EndpointsExigenSesionYValidanPaginaConDetalleCompletoTrasAlta()
    {
        using var anonimo = app.CrearCliente();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync("/api/proveedores")).StatusCode);
        using var cliente = await app.CrearClienteAutenticadoAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.GetAsync("/api/proveedores?pagina=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.GetAsync("/api/proveedores?tipo=desconocido")).StatusCode);
        var respuesta = await cliente.PostAsJsonAsync("/api/proveedores", new ProveedorPeticion("proveedorNormal", "Alta HTTP", Cuit(700)));
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var detalle = (await respuesta.Content.ReadFromJsonAsync<ProveedorDetalle>())!;
        Assert.Equal($"/api/proveedores/{detalle.Id}", respuesta.Headers.Location!.OriginalString);
        Assert.Equal("activo", detalle.Estado); Assert.Equal(1, detalle.Version);
        Assert.Equal(detalle, await cliente.GetFromJsonAsync<ProveedorDetalle>($"/api/proveedores/{detalle.Id}"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CheckRechazaRazonSocialNulaOVacia(string? razon)
    {
        await app.EnLaBaseAsync(async db =>
        {
            db.Proveedores.Add(EscenarioProveedores.Normal(Cuit(400), "Temporal"));
            db.ChangeTracker.Entries<Proveedor>().Single().Entity.RazonSocial = razon;
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Contains("CK_Proveedores_Identidad", error.InnerException!.Message);
        });
    }

    [Fact]
    public async Task EstadoVersionYContactoDelOrigenSeConservan()
    {
        await app.EnLaBaseAsync(async db =>
        {
            if (!await db.EmpresaEmisora.AnyAsync()) db.EmpresaEmisora.Add(EscenarioProveedores.Emisor());
            var externo = EscenarioProveedores.Externo();
            db.Transportistas.Add(externo);
            await db.SaveChangesAsync();
            var repo = new RepositorioProveedores(db);
            var normal = await repo.GuardarAsync(null, new("proveedorNormal", "Normal", "30712345671"), default);
            var baja = await repo.CambiarEstadoAsync(normal.Id, normal.Version, false, default);
            var editado = await repo.GuardarAsync(normal.Id, new("proveedorNormal", "Corregido", normal.Cuit, Version: baja.Version), default);
            Assert.Equal("inactivo", editado.Estado);
            Assert.Equal(baja.Version + 1, editado.Version);
            Assert.False(await db.Proveedores.Where(p => p.Id == normal.Id).Select(p => p.Activo).SingleAsync());
            var alta = await repo.CambiarEstadoAsync(normal.Id, editado.Version, true, default);
            Assert.Equal(alta.Version, (await repo.CambiarEstadoAsync(normal.Id, editado.Version, true, default)).Version);
            var fletero = await repo.GuardarAsync(null, new("fletero", TransportistaId: externo.Id), default);
            await db.Transportistas.Where(t => t.Id == externo.Id).ExecuteUpdateAsync(s => s.SetProperty(t => t.Nombre, "Origen actualizado"));
            var detalle = await repo.ObtenerAsync(fletero.Id, default);
            Assert.Equal("Origen actualizado", detalle!.RazonSocial);
            Assert.Equal(externo.Email, detalle.Email);
            Assert.Null(await db.Proveedores.Where(p => p.Id == fletero.Id).Select(p => p.Cuit).SingleAsync());
        });
    }

    [Fact]
    public async Task PaginaHomonimosYFiltraAntesDePaginarSinInterpretarPatrones()
    {
        await app.EnLaBaseAsync(async db =>
        {
            // CUIT únicos válidos derivados de la misma regla del padrón.
            for (var i = 0; i < 23; i++)
            {
                var proveedor = EscenarioProveedores.Normal(Cuit(i), "Homónimo US1");
                proveedor.NombreComercial = i == 0 ? "Especial" : null;
                proveedor.Activo = i != 0;
                db.Proveedores.Add(proveedor);
            }
            await db.SaveChangesAsync();
            var repo = new RepositorioProveedores(db);
            var primera = await repo.ConsultarAsync(new(Q: "Homónimo US1"), default);
            var segunda = await repo.ConsultarAsync(new(Q: "Homónimo US1", Pagina: 2), default);
            Assert.Equal(23, primera.Total);
            Assert.Equal(20, primera.Items.Count);
            Assert.Equal(3, segunda.Items.Count);
            Assert.True(primera.Items.Last().Id < segunda.Items.First().Id);
            Assert.Empty((await repo.ConsultarAsync(new(Q: "%"), default)).Items);
            Assert.Single((await repo.ConsultarAsync(new(Q: "Homónimo US1", NombreComercial: "Especial", Estado: "inactivo", Tipo: "proveedorNormal"), default)).Items);
        });
    }

    [Theory]
    [InlineData(201, 0)]
    [InlineData(200, 31)]
    public async Task CapacidadesDeBaseNoTruncan(int razon, int telefono)
    {
        await app.EnLaBaseAsync(async db =>
        {
            var proveedor = EscenarioProveedores.Normal(Cuit(500 + telefono), new string('R', razon));
            proveedor.Telefono = new string('1', telefono);
            db.Proveedores.Add(proveedor);
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Contains(Assert.IsType<Microsoft.Data.SqlClient.SqlException>(error.InnerException).Number, new[] { 2628, 8152 });
        });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task CheckRechazaCuitNuloOVacio(string? cuit)
    {
        await app.EnLaBaseAsync(async db =>
        {
            var proveedor = EscenarioProveedores.Normal(Cuit(410)); proveedor.Cuit = cuit;
            db.Proveedores.Add(proveedor);
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Contains("CK_Proveedores_Identidad", error.InnerException!.Message);
        });
    }

    [Fact]
    public async Task ExternosPaginadosIncluyenInactivosSinExponerContacto()
    {
        await app.EnLaBaseAsync(async db =>
        {
            if (!await db.EmpresaEmisora.AnyAsync()) db.EmpresaEmisora.Add(EscenarioProveedores.Emisor());
            for (var i = 0; i < 23; i++)
            {
                var externo = EscenarioProveedores.Externo(Cuit(600 + i)); externo.Nombre = "Selector US1"; externo.Activo = i != 22;
                db.Transportistas.Add(externo);
            }
            await db.SaveChangesAsync();
            var repo = new RepositorioProveedores(db);
            var primera = await repo.ExternosAsync("Selector US1", 1, default);
            var segunda = await repo.ExternosAsync("Selector US1", 2, default);
            Assert.Equal(23, primera.Total); Assert.Equal(20, primera.Items.Count); Assert.Equal(3, segunda.Items.Count);
            Assert.True(primera.Items.Last().Id < segunda.Items.First().Id);
            Assert.Null(typeof(TransportistaExterno).GetProperty("Email"));
            Assert.Null(typeof(TransportistaExterno).GetProperty("Telefono"));
        });
    }

    internal static string Cuit(int i)
    {
        var baseCuit = "30" + (80000000 + i).ToString();
        var pesos = new[] { 5, 4, 3, 2, 7, 6, 5, 4, 3, 2 };
        var digito = 11 - baseCuit.Select((c, j) => (c - '0') * pesos[j]).Sum() % 11;
        return baseCuit + (digito == 11 ? 0 : digito == 10 ? 9 : digito);
    }
}
