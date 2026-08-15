using GT.Domain.Facturacion;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Viajes;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Facturacion;

/// <summary>
/// Los dos índices únicos filtrados de <c>Facturas</c>, verificados fila por fila (research §4, §15.2).
///
/// <b>Por qué existe este test</b>: los dos filtros llevan el valor numérico de <see cref="EstadoFactura"/>
/// escrito a mano —<c>WHERE [Estado] &lt;&gt; 2</c>—. Reordenar el enum <b>no falla al compilar</b> y
/// dejaría el índice protegiendo el estado equivocado: el número de comprobante pasaría a ser único
/// entre las anuladas y dos facturas vigentes podrían compartirlo. Nada más en el sistema notaría la
/// diferencia hasta que alguien emitiera dos facturas con el mismo número.
///
/// Es el mismo test que el Módulo 5 escribió para sus tres índices filtrados: inserta una fila en cada
/// estado y verifica dónde el índice acepta y dónde rechaza.
///
/// Escribe contra la base <b>sin pasar por los casos de uso</b> a propósito: lo que se verifica es la
/// garantía de la base, no la validación de la aplicación. Con la validación en el medio, un índice mal
/// filtrado pasaría inadvertido.
/// </summary>
public class IndicesDeFacturaTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    /// <summary>
    /// El número es único entre las <b>no anuladas</b> (FR-027): las dos vigentes con el mismo número
    /// no pueden coexistir.
    /// </summary>
    [Theory]
    [InlineData(EstadoFactura.Pendiente, EstadoFactura.Pendiente)]
    [InlineData(EstadoFactura.Pendiente, EstadoFactura.Pagada)]
    [InlineData(EstadoFactura.Pagada, EstadoFactura.Pagada)]
    public async Task ElIndiceDelNumeroRechazaDosFacturasVigentesConElMismoNumero(
        EstadoFactura primera,
        EstadoFactura segunda)
    {
        var cliente = await app.CrearClienteAsync();
        var numero = DatosDePruebaFacturas.NumeroUnico();

        await app.CrearFacturaAsync(cliente.Id, numeroComprobante: numero, estado: primera);

        var choque = await Assert.ThrowsAsync<DbUpdateException>(() =>
            app.CrearFacturaAsync(cliente.Id, numeroComprobante: numero, estado: segunda));

        Assert.Contains("IX_Facturas_Numero", choque.InnerException!.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Anular <b>libera</b> el número: el filtro deja las anuladas fuera del índice, así que se puede
    /// reemitir con el mismo número. Es lo que hace posible el flujo anular → refacturar (FR-027).
    /// </summary>
    [Fact]
    public async Task ElIndiceDelNumeroAceptaReemitirElNumeroDeUnaAnulada()
    {
        var cliente = await app.CrearClienteAsync();
        var numero = DatosDePruebaFacturas.NumeroUnico();

        await app.CrearFacturaAsync(
            cliente.Id,
            numeroComprobante: numero,
            estado: EstadoFactura.Anulada,
            motivoAnulacion: "Datos del cliente equivocados.");

        var reemitida = await app.CrearFacturaAsync(
            cliente.Id,
            numeroComprobante: numero,
            estado: EstadoFactura.Pendiente);

        Assert.NotEqual(0, reemitida.Id);
    }

    /// <summary>
    /// Dos anuladas <b>sí</b> pueden compartir número, porque el índice las excluye a las dos. Es la
    /// salvedad que research §10 deja anotada: exige emitir, anular y reemitir con el mismo número, y
    /// el resultado no confunde a nadie.
    /// </summary>
    [Fact]
    public async Task ElIndiceDelNumeroAceptaDosAnuladasConElMismoNumero()
    {
        var cliente = await app.CrearClienteAsync();
        var numero = DatosDePruebaFacturas.NumeroUnico();

        await app.CrearFacturaAsync(
            cliente.Id,
            numeroComprobante: numero,
            estado: EstadoFactura.Anulada,
            motivoAnulacion: "Primera.");

        var segunda = await app.CrearFacturaAsync(
            cliente.Id,
            numeroComprobante: numero,
            estado: EstadoFactura.Anulada,
            motivoAnulacion: "Segunda.");

        Assert.NotEqual(0, segunda.Id);
    }

    /// <summary>
    /// FR-049a: a una factura anulada la reemplaza <b>a lo sumo una</b> Refacturación. El índice es lo
    /// que lo sostiene ante dos operadores simultáneos, no la consulta previa.
    /// </summary>
    [Fact]
    public async Task ElIndiceDeRefacturacionRechazaDosQueReemplacenALaMismaAnulada()
    {
        var cliente = await app.CrearClienteAsync();

        var anulada = await app.CrearFacturaAsync(
            cliente.Id,
            estado: EstadoFactura.Anulada,
            motivoAnulacion: "Importes mal cargados.");

        await app.CrearFacturaAsync(
            cliente.Id,
            tipoFacturacion: TipoFacturacion.Refacturacion,
            facturaReemplazadaId: anulada.Id);

        var choque = await Assert.ThrowsAsync<DbUpdateException>(() =>
            app.CrearFacturaAsync(
                cliente.Id,
                tipoFacturacion: TipoFacturacion.Refacturacion,
                facturaReemplazadaId: anulada.Id));

        Assert.Contains(
            "IX_Facturas_FacturaReemplazada",
            choque.InnerException!.Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// El filtro <c>IS NOT NULL</c> es lo que permite que <b>todas</b> las facturas Original convivan:
    /// sin él, la segunda con <c>FacturaReemplazadaId</c> nulo chocaría contra la primera y el módulo
    /// entero dejaría de funcionar.
    /// </summary>
    [Fact]
    public async Task ElIndiceDeRefacturacionAceptaMuchasFacturasSinReferencia()
    {
        var cliente = await app.CrearClienteAsync();

        await app.CrearFacturaAsync(cliente.Id);
        await app.CrearFacturaAsync(cliente.Id);
        var tercera = await app.CrearFacturaAsync(cliente.Id);

        Assert.NotEqual(0, tercera.Id);
    }

    /// <summary>
    /// El <c>CHECK</c> del total no es decoración: una fila con el total inconsistente no tiene que
    /// poder existir, aunque el cálculo viva en el dominio.
    ///
    /// Espera <see cref="SqlException"/> y no <c>DbUpdateException</c>: <c>ExecuteUpdateAsync</c>
    /// manda el <c>UPDATE</c> sin pasar por el rastreador de cambios, así que el error de la base
    /// llega tal cual en vez de envuelto.
    /// </summary>
    [Fact]
    public async Task LaBaseRechazaUnTotalQueNoEsLaSumaDelNetoYElIva()
    {
        var cliente = await app.CrearClienteAsync();
        var factura = await app.CrearFacturaAsync(cliente.Id);

        var choque = await Assert.ThrowsAsync<SqlException>(() => app.EnLaBaseAsync(async contexto =>
        {
            await contexto.Facturas
                .Where(f => f.Id == factura.Id)
                .ExecuteUpdateAsync(cambio => cambio.SetProperty(f => f.Total, 1m));
        }));

        Assert.Contains("CK_Facturas_Total", choque.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// El <c>CHECK ([Id] = 1)</c> de <c>EmpresaEmisora</c>: la configuración es única para todo el
    /// sistema y lo garantiza la base, no la disciplina del código (FR-001, research §12).
    /// </summary>
    [Fact]
    public async Task LaBaseRechazaUnaSegundaFilaDeEmpresaEmisora()
    {
        await app.ConfigurarEmpresaEmisoraAsync();

        var choque = await Assert.ThrowsAsync<DbUpdateException>(() => app.EnLaBaseAsync(async contexto =>
        {
            contexto.EmpresaEmisora.Add(new EmpresaEmisora
            {
                Id = 2,
                RazonSocial = "Otra empresa",
                Cuit = "30712345670",
                Domicilio = "Otro domicilio",
                CondicionIva = "IVA Responsable Inscripto",
            });

            await contexto.SaveChangesAsync();
        }));

        Assert.Contains(
            "CK_EmpresaEmisora_FilaUnica",
            choque.InnerException!.Message,
            StringComparison.Ordinal);
    }
}
