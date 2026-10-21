using System.Net;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Caja;
using GT.IntegrationTests.Facturacion;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Pagos;
using GT.IntegrationTests.Viajes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Reportes;

public class SinMutacionesExportacionTests
{
    [Fact]
    public async Task TodosLosArchivosConservanTablasEconomicasEHistoria_YNoPersistenArchivos()
    {
        await using var app = new AplicacionAnalisisDePrueba(); await app.InitializeAsync();
        await EscenarioAnulacion.CrearAsync(app, "fletero");
        var cliente = await app.CrearClienteAsync(); await app.CrearFacturaAsync(cliente.Id, fecha: new(2026, 10, 10));
        var usuario = await app.UsuarioAsync(); var caja = await app.CrearCajaAsync(usuario.Id);
        await app.CrearMovimientoAsync(caja.Id, usuario.Id, fecha: new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc));
        var antes = await FotoAsync(app);
        var archivos = Directory.Exists(app.RutaDeArchivos) ? Directory.GetFiles(app.RutaDeArchivos, "*", SearchOption.AllDirectories).Order().ToArray() : [];
        using var http = await app.CrearClienteAutenticadoAsync();
        foreach (var recurso in RutasExportacionAnalisisTests.Recursos)
        foreach (var formato in new[] { "pdf", "excel" })
        {
            using var respuesta = await http.GetAsync(RutasExportacionAnalisisTests.Url(recurso, formato));
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        }
        var despues = await FotoAsync(app);
        Assert.Equal(antes.Keys.Order(), despues.Keys.Order());
        foreach (var tabla in antes.Keys) Assert.Equal(antes[tabla], despues[tabla]);
        var posteriores = Directory.Exists(app.RutaDeArchivos) ? Directory.GetFiles(app.RutaDeArchivos, "*", SearchOption.AllDirectories).Order().ToArray() : [];
        Assert.Equal(archivos, posteriores);
    }

    private static async Task<Dictionary<string, string>> FotoAsync(AplicacionDePrueba app)
    {
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<GtDbContext>();
        var modulos = new[] { "Viajes", "Facturacion", "Liquidaciones", "Caja", "Pagos", "Proveedores", "Flota", "Choferes" };
        var tablas = db.Model.GetEntityTypes().Where(e => modulos.Any(m => e.ClrType.Namespace?.StartsWith("GT.Domain." + m, StringComparison.Ordinal) == true))
            .Where(e => e.GetTableName() is not null).GroupBy(e => (Esquema: e.GetSchema() ?? "dbo", Tabla: e.GetTableName()!));
        var resultado = new Dictionary<string, string>(); var conexion = db.Database.GetDbConnection(); await conexion.OpenAsync();
        foreach (var grupo in tablas)
        {
            var entidad = grupo.First(); var almacen = StoreObjectIdentifier.Table(grupo.Key.Tabla, entidad.GetSchema());
            var orden = string.Join(",", entidad.FindPrimaryKey()!.Properties.Select(p => "[" + p.GetColumnName(almacen)!.Replace("]", "]]", StringComparison.Ordinal) + "]"));
            await using var comando = conexion.CreateCommand();
            comando.CommandText = $"SELECT * FROM [{grupo.Key.Esquema}].[{grupo.Key.Tabla}] ORDER BY {orden} FOR JSON PATH, INCLUDE_NULL_VALUES";
            // Sólo datos sintéticos en memoria del test; no se guardan dumps ni incluyen usuarios/credenciales.
            await using var lector = await comando.ExecuteReaderAsync(); var json = new System.Text.StringBuilder();
            while (await lector.ReadAsync()) json.Append(lector.GetString(0));
            resultado.Add(grupo.Key.Tabla, json.ToString());
        }
        return resultado;
    }
}
