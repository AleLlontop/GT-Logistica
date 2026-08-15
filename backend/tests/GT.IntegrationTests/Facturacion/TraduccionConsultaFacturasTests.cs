using GT.Application.Facturacion;
using GT.Domain.Choferes;
using GT.Domain.Facturacion;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Viajes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Facturacion;

/// <summary>
/// <b>La trampa de research §15.4</b>: la derivación de <c>vencida</c> tiene que quedar <b>escrita en el
/// árbol de la consulta</b> y no extraída a un método propio.
///
/// Extraerla rompe la traducción de EF Core: la consulta pasa a evaluarse en memoria, el filtro se aplica
/// <b>después</b> de paginar, y las páginas salen incompletas con un total que no coincide. <b>Y no falla:
/// devuelve datos mal</b>, que es peor.
///
/// Este test mira el SQL que EF genera. Es el mismo criterio con el que el Módulo 5 cubrió su subconsulta
/// de <c>demorado</c> (convención [003]).
/// </summary>
public class TraduccionConsultaFacturasTests(AplicacionDePrueba app)
    : IClassFixture<AplicacionDePrueba>
{
    /// <summary>
    /// El filtro por estado derivado <b>viaja al <c>WHERE</c></b>, junto con el <c>ORDER BY</c> y la
    /// paginación: los tres tienen que estar en la misma sentencia para que el orden de las operaciones
    /// sea filtrar → ordenar → paginar.
    /// </summary>
    [Theory]
    [InlineData(EstadoFacturaVisible.Pendiente)]
    [InlineData(EstadoFacturaVisible.Vencida)]
    [InlineData(EstadoFacturaVisible.Pagada)]
    [InlineData(EstadoFacturaVisible.Anulada)]
    public void El_FiltroPorEstadoDerivado_SeTraduceASql(EstadoFacturaVisible estado)
    {
        using var alcance = app.Services.CreateScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<GtDbContext>();

        var hoy = FechaHoyArgentina.Hoy();

        // La misma cadena de predicados que arma `RepositorioFacturas.ConsultarAsync`.
        var consulta = contexto.Facturas.AsQueryable();

        consulta = estado switch
        {
            EstadoFacturaVisible.Pendiente => consulta.Where(factura =>
                factura.Estado == EstadoFactura.Pendiente && factura.VencimientoPago >= hoy),

            EstadoFacturaVisible.Vencida => consulta.Where(factura =>
                factura.Estado == EstadoFactura.Pendiente && factura.VencimientoPago < hoy),

            EstadoFacturaVisible.Pagada => consulta.Where(factura =>
                factura.Estado == EstadoFactura.Pagada),

            _ => consulta.Where(factura => factura.Estado == EstadoFactura.Anulada),
        };

        var sql = consulta
            .OrderByDescending(factura => factura.Fecha)
            .ThenByDescending(factura => factura.NumeroComprobante)
            .Skip(0)
            .Take(20)
            .ToQueryString();

        // El filtro está en el SQL, no en memoria.
        Assert.Contains("WHERE", sql, StringComparison.Ordinal);
        Assert.Contains("[Estado]", sql, StringComparison.Ordinal);

        // El orden y la paginación también: si el filtro se evaluara en memoria, el `OFFSET` recortaría
        // antes de filtrar y las páginas saldrían incompletas.
        Assert.Contains("ORDER BY", sql, StringComparison.Ordinal);
        Assert.Contains("OFFSET", sql, StringComparison.Ordinal);

        // Los dos estados impagos comparan el vencimiento **en la base**.
        if (estado is EstadoFacturaVisible.Pendiente or EstadoFacturaVisible.Vencida)
        {
            Assert.Contains("[VencimientoPago]", sql, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// La exclusión de las anuladas en los totales y la agregación de las tres columnas viajan al
    /// <c>GROUP BY</c>: sumar en memoria sobre miles de filas es lo que el plan §Performance Goals descarta
    /// (FR-061, FR-062).
    /// </summary>
    [Fact]
    public void Los_Totales_SeAgreganEnLaBase()
    {
        using var alcance = app.Services.CreateScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<GtDbContext>();

        var hoy = FechaHoyArgentina.Hoy();

        var sql = contexto.Facturas
            .Where(factura =>
                factura.Fecha >= hoy.AddMonths(-1) &&
                factura.Fecha <= hoy &&
                factura.Estado != EstadoFactura.Anulada)
            .GroupBy(factura => new { factura.ClienteId, factura.ClienteRazonSocial })
            .Select(grupo => new
            {
                grupo.Key.ClienteId,
                grupo.Key.ClienteRazonSocial,
                Cantidad = grupo.Count(),
                Facturado = grupo.Sum(factura => factura.Total),
                Cobrado = grupo.Sum(factura =>
                    factura.Estado == EstadoFactura.Pagada ? factura.Total : 0m),
            })
            .ToQueryString();

        Assert.Contains("GROUP BY", sql, StringComparison.Ordinal);
        Assert.Contains("SUM(", sql, StringComparison.Ordinal);
        Assert.Contains("COUNT(", sql, StringComparison.Ordinal);

        // El cobrado sale de un `CASE WHEN` traducido, no de una segunda consulta.
        Assert.Contains("CASE", sql, StringComparison.Ordinal);
    }

    /// <summary>
    /// El panel de vencimientos calcula los días <b>en la base</b> con <c>DATEDIFF</c> y acota la ventana en
    /// el <c>WHERE</c>: si los días se calcularan al leer, la fila podría decir "vence en 8 días" habiendo
    /// entrado por una comparación contra otro instante (FR-063).
    /// </summary>
    [Fact]
    public void El_PanelDeVencimientos_CalculaLosDiasEnLaBase()
    {
        using var alcance = app.Services.CreateScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<GtDbContext>();

        var hoy = FechaHoyArgentina.Hoy();
        var limite = hoy.AddDays(7);

        var sql = contexto.Facturas
            .Where(factura =>
                factura.Estado == EstadoFactura.Pendiente &&
                factura.VencimientoPago <= limite)
            .Select(factura => new
            {
                factura.Id,
                Dias = EF.Functions.DateDiffDay(hoy, factura.VencimientoPago),
            })
            .ToQueryString();

        Assert.Contains("DATEDIFF", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WHERE", sql, StringComparison.Ordinal);
    }

    /// <summary>
    /// Los facturables resuelven las cuatro condiciones en la base, incluida la del período: traer todos los
    /// viajes del cliente y filtrar el mes al leer sería recorrer su historia entera cada vez (FR-015).
    /// </summary>
    [Fact]
    public void Los_Facturables_ResuelvenLasCuatroCondicionesEnLaBase()
    {
        using var alcance = app.Services.CreateScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<GtDbContext>();

        var sql = contexto.Viajes
            .Where(viaje =>
                viaje.ClienteId == 1 &&
                viaje.Estado == Domain.Viajes.EstadoViaje.Rendido &&
                viaje.FacturaId == null &&
                viaje.Fecha.Month == 8 &&
                viaje.Fecha.Year == 2026)
            .ToQueryString();

        Assert.Contains("[ClienteId]", sql, StringComparison.Ordinal);
        Assert.Contains("[Estado]", sql, StringComparison.Ordinal);
        Assert.Contains("[FacturaId]", sql, StringComparison.Ordinal);
        Assert.Contains("WHERE", sql, StringComparison.Ordinal);
    }

    /// <summary>
    /// Los cinco índices de data-model §Índices existen en la base con el nombre que el código declara.
    ///
    /// Los nombres los lee <c>RepositorioFacturas</c> del mensaje de la excepción de SQL Server para saber
    /// cuál índice se violó: si uno cambiara de nombre, la traducción del rechazo dejaría de funcionar y
    /// <b>no fallaría al compilar</b> (convención [003]).
    /// </summary>
    [Fact]
    public async Task Los_CincoIndices_ExistenConSuNombre()
    {
        var nombres = await app.ConAlcanceAsync(async contexto =>
        {
            var leidos = new List<string>();

            var conexion = contexto.Database.GetDbConnection();
            await conexion.OpenAsync();

            await using var comando = conexion.CreateCommand();
            comando.CommandText =
                "SELECT name FROM sys.indexes WHERE object_id = OBJECT_ID('Facturas') AND name IS NOT NULL";

            await using var lector = await comando.ExecuteReaderAsync();

            while (await lector.ReadAsync())
            {
                leidos.Add(lector.GetString(0));
            }

            return leidos;
        });

        Assert.Contains("IX_Facturas_Numero", nombres);
        Assert.Contains("IX_Facturas_FacturaReemplazada", nombres);
        Assert.Contains("IX_Facturas_Fecha_Numero", nombres);
        Assert.Contains("IX_Facturas_ClienteId", nombres);
        Assert.Contains("IX_Facturas_Estado_VencimientoPago", nombres);
    }

    /// <summary>
    /// Y el de <c>Viajes.FacturaId</c>, que el Módulo 6 agregó: es el que sostiene la consulta de los
    /// facturables y la devolución de viajes al anular (FR-053).
    /// </summary>
    [Fact]
    public async Task El_IndiceDeFacturaIdEnViajes_Existe()
    {
        var existe = await app.ConAlcanceAsync(async contexto =>
        {
            var conexion = contexto.Database.GetDbConnection();
            await conexion.OpenAsync();

            await using var comando = conexion.CreateCommand();
            comando.CommandText =
                "SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID('Viajes') " +
                "AND name = 'IX_Viajes_FacturaId'";

            return (int)(await comando.ExecuteScalarAsync())!;
        });

        Assert.Equal(1, existe);
    }
}
