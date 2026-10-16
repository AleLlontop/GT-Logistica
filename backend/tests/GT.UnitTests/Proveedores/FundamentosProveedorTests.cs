using System.Security.Claims;
using GT.Api.Autenticacion;
using GT.Api.Autorizacion;
using GT.Domain.Proveedores;
using GT.Domain.Usuarios;
using GT.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace GT.UnitTests.Proveedores;

public class FundamentosProveedorTests
{
    [Fact]
    public void MetadataDeArchivoTieneLasCapacidadesDelAlmacenExistente()
    {
        using var contexto = Contexto();
        var factura = contexto.Model.FindEntityType(typeof(FacturaProveedor))!;
        Assert.Equal(400, factura.FindProperty(nameof(FacturaProveedor.ArchivoRuta))!.GetMaxLength());
        Assert.Equal(255, factura.FindProperty(nameof(FacturaProveedor.ArchivoNombre))!.GetMaxLength());
        Assert.Equal(100, factura.FindProperty(nameof(FacturaProveedor.ArchivoTipoContenido))!.GetMaxLength());
        Assert.Equal(18, factura.FindProperty(nameof(FacturaProveedor.Neto))!.GetPrecision());
        Assert.Equal(2, factura.FindProperty(nameof(FacturaProveedor.Neto))!.GetScale());
    }

    [Fact]
    public void UnicidadNoDependeDelEstadoDelProveedor()
    {
        using var contexto = Contexto();
        var proveedor = contexto.Model.FindEntityType(typeof(Proveedor))!;
        var cuit = Assert.Single(proveedor.GetIndexes(), indice => indice.Properties.Single().Name == "Cuit");
        Assert.True(cuit.IsUnique);
        Assert.Equal("[Cuit] IS NOT NULL", cuit.GetFilter());
        Assert.True(proveedor.FindProperty("Version")!.IsConcurrencyToken);
        Assert.Equal(true, proveedor.FindProperty("Activo")!.Sentinel);
    }

    [Fact]
    public void AsociacionYArchivoCierranElCasoNullEnSql()
    {
        using var contexto = Contexto();
        var factura = contexto.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(FacturaProveedor))!;
        var asociacion = factura.GetCheckConstraints().Single(c => c.Name == "CK_FacturasProveedor_Liquidacion").Sql;
        Assert.Contains("[ImporteLiquidacionAlRegistrar] IS NOT NULL", asociacion);
        var archivo = factura.GetCheckConstraints().Single(c => c.Name == "CK_FacturasProveedor_Archivo").Sql;
        Assert.Contains("[ArchivoTipoContenido] IS NOT NULL", archivo);
        Assert.All(factura.GetForeignKeys(), clave => Assert.Equal(DeleteBehavior.Restrict, clave.DeleteBehavior));
    }

    [Theory]
    [InlineData(CodigosPermiso.ProveedoresGestionar)]
    [InlineData(CodigosPermiso.ProveedoresFacturasCargar)]
    [InlineData(CodigosPermiso.ProveedoresFacturasConsultar)]
    public async Task CadaPermisoSoloAutorizaSuPoliticaYLaBusquedaMinima(string permiso)
    {
        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddAuthorization(opciones => opciones.AgregarPoliticasDeProveedores());
        servicios.AddSingleton<IAuthorizationHandler, PermisoHandler>();
        using var proveedor = servicios.BuildServiceProvider();
        var autorizacion = proveedor.GetRequiredService<IAuthorizationService>();
        var usuario = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimsSesion.TipoPermiso, permiso)], "prueba"));
        foreach (var codigo in new[] { CodigosPermiso.ProveedoresGestionar, CodigosPermiso.ProveedoresFacturasCargar, CodigosPermiso.ProveedoresFacturasConsultar })
            Assert.Equal(codigo == permiso, (await autorizacion.AuthorizeAsync(usuario, null, PoliticasAutorizacion.Para(codigo))).Succeeded);
        Assert.True((await autorizacion.AuthorizeAsync(usuario, null, PoliticasAutorizacion.BuscarProveedores)).Succeeded);
        Assert.False((await autorizacion.AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity()), null, PoliticasAutorizacion.BuscarProveedores)).Succeeded);
    }

    private static GtDbContext Contexto() => new(new DbContextOptionsBuilder<GtDbContext>()
        .UseSqlServer("Server=(local);Database=SoloMetadata;Integrated Security=True;TrustServerCertificate=True").Options);
}
