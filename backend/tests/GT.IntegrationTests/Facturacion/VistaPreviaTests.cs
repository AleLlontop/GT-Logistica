using System.Net;
using System.Net.Http.Json;
using GT.Domain.Choferes;
using GT.Domain.Viajes;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Viajes;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Facturacion;

/// <summary>
/// SC-007b: <b>la vista previa y el documento guardado coinciden byte a byte</b>.
///
/// <b>Esto no lo puede verificar una persona</b>: a ojo se comparan los bloques, y dos maquetas que se
/// separaron en un detalle chico se ven iguales. La igualdad exacta la verifica este test, y el
/// <c>quickstart.md</c> lo declara en vez de pedirle a quien valida algo que no puede hacer
/// (research §14).
///
/// Es lo que hace verdadero al diseño de FR-033: <b>un único armador sobre la misma entrada</b>. Si
/// alguien agregara una segunda maqueta —o construyera un DTO distinto por camino—, este test se pone
/// rojo. Sin él, las dos se separarían sin que nadie lo note y revisar la vista previa dejaría de servir
/// para algo (research §2).
/// </summary>
public class VistaPreviaTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task La_VistaPrevia_YElDocumentoGuardado_SonIgualesByteAByte()
    {
        var (clienteId, viajes) = await EscenarioAsync();
        var cliente = await app.CrearClienteAutenticadoAsync();

        // **El mismo cuerpo para los dos caminos.** Es la condición del test: si la vista previa
        // recibiera algo distinto de lo que se emite, comparar los PDF no probaría nada.
        var cuerpo = Cuerpo(clienteId, viajes.Select(v => v.Id).ToArray());

        var vistaPrevia = await cliente.PostAsJsonAsync("/api/facturas/vista-previa", cuerpo);

        Assert.Equal(HttpStatusCode.OK, vistaPrevia.StatusCode);
        Assert.Equal("application/pdf", vistaPrevia.Content.Headers.ContentType?.MediaType);

        var bytesDeLaVistaPrevia = await vistaPrevia.Content.ReadAsByteArrayAsync();

        var emision = await cliente.PostAsJsonAsync("/api/facturas", cuerpo);
        Assert.Equal(HttpStatusCode.Created, emision.StatusCode);

        var emitida = await emision.Content.ReadFromJsonAsync<FacturaDetalleLeida>();

        var documento = await cliente.GetAsync(emitida!.DocumentoUrl);
        var bytesDelDocumento = await documento.Content.ReadAsByteArrayAsync();

        Assert.Equal(bytesDeLaVistaPrevia, bytesDelDocumento);
    }

    /// <summary>
    /// US2 esc. 33: pedir la vista previa <b>no crea la factura ni escribe ningún archivo</b>. Una vista
    /// previa abandonada no deja rastro.
    ///
    /// Se cuentan los archivos del volumen antes y después: es la única forma de verificar que no se
    /// escribió nada, porque un archivo huérfano es invisible desde la aplicación.
    /// </summary>
    [Fact]
    public async Task Pedir_LaVistaPrevia_NoCreaLaFacturaNiEscribeNingunArchivo()
    {
        var (clienteId, viajes) = await EscenarioAsync();
        var cliente = await app.CrearClienteAutenticadoAsync();

        var archivosAntes = ContarArchivosDelVolumen();

        for (var intento = 0; intento < 3; intento++)
        {
            var respuesta = await cliente.PostAsJsonAsync(
                "/api/facturas/vista-previa",
                Cuerpo(clienteId, viajes.Select(v => v.Id).ToArray()));

            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        }

        Assert.Equal(0, await app.ConAlcanceAsync(contexto =>
            contexto.Facturas.CountAsync(factura => factura.ClienteId == clienteId)));

        Assert.Equal(archivosAntes, ContarArchivosDelVolumen());

        // Y los viajes siguen intactos y disponibles para facturar.
        foreach (var viaje in viajes)
        {
            var despues = await app.RecargarViajeAsync(viaje.Id);

            Assert.Equal(EstadoViaje.Rendido, despues!.Estado);
            Assert.Null(despues.FacturaId);
        }
    }

    /// <summary>
    /// La vista previa aplica <b>las mismas validaciones de datos</b> que la emisión: un documento que no
    /// se puede emitir tampoco se puede previsualizar honestamente
    /// (contracts/facturacion-api.yaml §vista-previa).
    /// </summary>
    [Fact]
    public async Task La_VistaPrevia_AplicaLasMismasValidacionesDeDatos()
    {
        var (clienteId, viajes) = await EscenarioAsync(conRemito: false);
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/facturas/vista-previa",
            Cuerpo(clienteId, viajes.Select(v => v.Id).ToArray()));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorFacturaLeido>();
        Assert.Equal("viaje_sin_remito", error!.Codigo);
    }

    /// <summary>
    /// Y <b>no</b> aplica las confirmaciones de FR-032: no hay nada irreversible que confirmar todavía.
    /// Un viaje en cero se previsualiza sin pedir nada, y la confirmación llega al emitir.
    /// </summary>
    [Fact]
    public async Task La_VistaPrevia_NoPideLasConfirmacionesDeLaEmision()
    {
        var (clienteId, viajes) = await EscenarioAsync(importe: 0m);
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/facturas/vista-previa",
            Cuerpo(clienteId, viajes.Select(v => v.Id).ToArray()));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("application/pdf", respuesta.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>
    /// Y <b>tampoco</b> el número duplicado: previsualizar no reserva el número, así que frenar por eso
    /// sería cortar antes de tiempo algo que quien opera todavía puede corregir en el formulario.
    /// </summary>
    [Fact]
    public async Task La_VistaPrevia_NoRechazaPorNumeroDuplicado()
    {
        var (clienteId, viajes) = await EscenarioAsync(cantidadDeViajes: 2);
        var cliente = await app.CrearClienteAutenticadoAsync();
        var numero = DatosDePruebaFacturas.NumeroUnico();

        await cliente.PostAsJsonAsync("/api/facturas", Cuerpo(clienteId, [viajes[0].Id], numero));

        var vistaPrevia = await cliente.PostAsJsonAsync(
            "/api/facturas/vista-previa",
            Cuerpo(clienteId, [viajes[1].Id], numero));

        Assert.Equal(HttpStatusCode.OK, vistaPrevia.StatusCode);
    }

    /// <summary>
    /// Los archivos del volumen de la corrida, contados a mano.
    ///
    /// El almacén no expone un listado —a la aplicación no le hace falta— y un archivo huérfano es
    /// invisible desde adentro, así que la única forma de comprobar que la vista previa no escribió nada
    /// es mirar el directorio.
    /// </summary>
    private int ContarArchivosDelVolumen() =>
        Directory.Exists(app.RutaDeArchivos)
            ? Directory.GetFiles(app.RutaDeArchivos, "*", SearchOption.AllDirectories).Length
            : 0;

    private async Task<(int ClienteId, Viaje[] Viajes)> EscenarioAsync(
        int cantidadDeViajes = 2,
        decimal importe = 45_000m,
        bool conRemito = true)
    {
        await app.ConfigurarEmpresaEmisoraAsync();

        var padron = await app.CrearClienteAsync();

        await app.EnLaBaseAsync(async contexto =>
        {
            await contexto.Clientes
                .Where(c => c.Id == padron.Id)
                .ExecuteUpdateAsync(cambio =>
                    cambio.SetProperty(c => c.Direccion, "Ruta 9 km 312, Rosario"));
        });

        var viajes = new List<Viaje>();

        for (var i = 0; i < cantidadDeViajes; i++)
        {
            viajes.Add(await app.CrearViajeAsync(
                padron.Id,
                estado: EstadoViaje.Rendido,
                numeroRemito: conRemito ? ArmadoDeEscenarios.RemitoUnico() : null,
                importe: importe));
        }

        return (padron.Id, [.. viajes]);
    }

    private static object Cuerpo(int clienteId, int[] viajeIds, string? numero = null)
    {
        var hoy = FechaHoyArgentina.Hoy();

        return new
        {
            clienteId,
            tipoComprobante = "facturaA",
            tipoFacturacion = "original",
            condicionDeVenta = "cuentaCorriente",
            mes = hoy.Month,
            anio = hoy.Year,
            fecha = hoy.ToString("yyyy-MM-dd"),
            numeroComprobante = numero ?? DatosDePruebaFacturas.NumeroUnico(),
            detalle = "Servicios de transporte del período.",
            cae = "75123456789012",
            caeVencimiento = hoy.AddDays(10).ToString("yyyy-MM-dd"),
            vencimientoPago = hoy.AddDays(30).ToString("yyyy-MM-dd"),
            viajeIds,
        };
    }
}
