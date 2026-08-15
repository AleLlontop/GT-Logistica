using System.Net;
using System.Net.Http.Json;
using GT.Domain.Choferes;
using GT.Domain.Facturacion;
using GT.Domain.Viajes;
using GT.IntegrationTests.Choferes;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Usuarios;
using GT.IntegrationTests.Viajes;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Facturacion;

/// <summary>
/// La emisión de punta a punta (US2, FR-014, FR-054).
///
/// <b>O pasa todo o no pasa nada</b>: la factura, los viajes marcados, el historial y el documento en
/// una sola operación. Los tests que verifican que <i>no</i> pasa nada son la mitad del valor de este
/// archivo: un rechazo que deja la factura creada a medias es peor que un rechazo.
/// </summary>
public class EmisionFacturaTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    private async Task<(int ClienteId, Viaje[] Viajes)> EscenarioAsync(
        int cantidadDeViajes = 2,
        decimal importe = 50_000m,
        bool conRemito = true,
        string? domicilio = "Ruta 9 km 312, Rosario",
        DateOnly? fecha = null)
    {
        await app.ConfigurarEmpresaEmisoraAsync();

        var cliente = await app.CrearClienteAsync();

        if (domicilio is not null)
        {
            await app.EnLaBaseAsync(async contexto =>
            {
                await contexto.Clientes
                    .Where(c => c.Id == cliente.Id)
                    .ExecuteUpdateAsync(cambio => cambio.SetProperty(c => c.Direccion, domicilio));
            });
        }

        var viajes = new List<Viaje>();

        for (var i = 0; i < cantidadDeViajes; i++)
        {
            viajes.Add(await app.CrearViajeAsync(
                cliente.Id,
                fecha: fecha ?? FechaHoyArgentina.Hoy(),
                estado: EstadoViaje.Rendido,
                numeroRemito: conRemito ? ArmadoDeEscenarios.RemitoUnico() : null,
                importe: importe));
        }

        return (cliente.Id, [.. viajes]);
    }

    private static object Cuerpo(
        int clienteId,
        IEnumerable<int> viajeIds,
        string? numero = null,
        DateOnly? fecha = null,
        string tipoComprobante = "facturaA",
        string tipoFacturacion = "original",
        int? facturaReemplazadaId = null,
        bool? confirmado = null,
        DateOnly? vencimientoPago = null,
        DateOnly? caeVencimiento = null)
    {
        var fechaFactura = fecha ?? FechaHoyArgentina.Hoy();

        return new
        {
            clienteId,
            tipoComprobante,
            tipoFacturacion,
            condicionDeVenta = "cuentaCorriente",
            mes = fechaFactura.Month,
            anio = fechaFactura.Year,
            fecha = fechaFactura.ToString("yyyy-MM-dd"),
            numeroComprobante = numero ?? DatosDePruebaFacturas.NumeroUnico(),
            detalle = "Servicios de transporte del período.",
            cae = "75123456789012",
            caeVencimiento = (caeVencimiento ?? fechaFactura.AddDays(10)).ToString("yyyy-MM-dd"),
            vencimientoPago = (vencimientoPago ?? fechaFactura.AddDays(30)).ToString("yyyy-MM-dd"),
            facturaReemplazadaId,
            viajeIds = viajeIds.ToArray(),
            confirmado,
        };
    }

    // ── El camino feliz ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// La emisión deja los viajes en <c>facturado</c> con su <c>FacturaId</c>, escribe la entrada de
    /// emisión en el historial de la factura y <b>una línea de <c>CambioDeEstadoViaje</c> por viaje</b>.
    ///
    /// Esas líneas no las pide ninguna FR del Módulo 6: las exige FR-035 del Módulo 5, ya vigente, y sin
    /// ellas la ficha del viaje mostraría <c>facturado</c> sin una línea que lo explique (research §8).
    /// </summary>
    [Fact]
    public async Task La_Emision_MarcaLosViajesYEscribeLosDosHistoriales()
    {
        var (clienteId, viajes) = await EscenarioAsync();
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/facturas",
            Cuerpo(clienteId, viajes.Select(v => v.Id)));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var emitida = await respuesta.Content.ReadFromJsonAsync<FacturaDetalleLeida>();
        Assert.NotNull(emitida);
        Assert.Equal("pendiente", emitida!.Estado);
        Assert.Equal(2, emitida.Viajes.Count);

        // Los importes los calculó el servidor a partir de los viajes de la base (FR-024).
        Assert.Equal(100_000m, emitida.Neto);
        Assert.Equal(21_000m, emitida.Iva);
        Assert.Equal(121_000m, emitida.Total);
        Assert.Equal(21m, emitida.Alicuota);

        foreach (var viaje in viajes)
        {
            var despues = await app.RecargarViajeAsync(viaje.Id);

            Assert.Equal(EstadoViaje.Facturado, despues!.Estado);
            Assert.Equal(emitida.Id, despues.FacturaId);

            var historialDelViaje = await app.HistorialDeAsync(viaje.Id);
            Assert.Contains(
                historialDelViaje,
                linea => linea.EstadoAnterior == EstadoViaje.Rendido &&
                    linea.EstadoNuevo == EstadoViaje.Facturado);
        }

        // Toda factura tiene al menos una entrada: la de su emisión, con `EstadoAnterior` en nulo.
        var historial = await app.HistorialDeFacturaAsync(emitida.Id);
        var emision = Assert.Single(historial);

        Assert.Null(emision.EstadoAnterior);
        Assert.Equal(EstadoFactura.Pendiente, emision.EstadoNuevo);
        Assert.Equal(DateTimeKind.Utc, emision.OcurridoEn.Kind);
    }

    /// <summary>
    /// FR-034 y FR-034a: los trece datos quedan congelados. Cambiar después la configuración o el padrón
    /// <b>no altera la factura</b> (SC-007, US3 esc. 12).
    /// </summary>
    [Fact]
    public async Task La_Emision_CongelaLosDatosDelEmisorYDelCliente()
    {
        var (clienteId, viajes) = await EscenarioAsync(cantidadDeViajes: 1);
        var cliente = await app.CrearClienteAutenticadoAsync();

        var emitida = await (await cliente.PostAsJsonAsync(
                "/api/facturas",
                Cuerpo(clienteId, viajes.Select(v => v.Id))))
            .Content.ReadFromJsonAsync<FacturaDetalleLeida>();

        var razonSocialOriginal = emitida!.Cliente.RazonSocial;
        var domicilioDelEmisorOriginal = emitida.Emisor.Domicilio;

        // Se cambian las dos fuentes después de emitir.
        await app.EnLaBaseAsync(async contexto =>
        {
            await contexto.Clientes
                .Where(c => c.Id == clienteId)
                .ExecuteUpdateAsync(cambio => cambio
                    .SetProperty(c => c.RazonSocial, "Razón social cambiada después")
                    .SetProperty(c => c.Direccion, "Domicilio cambiado después"));

            await contexto.EmpresaEmisora.ExecuteUpdateAsync(cambio =>
                cambio.SetProperty(e => e.Domicilio, "Domicilio del emisor cambiado después"));
        });

        var relectura = await cliente.GetFromJsonAsync<FacturaDetalleLeida>(
            $"/api/facturas/{emitida.Id}");

        Assert.Equal(razonSocialOriginal, relectura!.Cliente.RazonSocial);
        Assert.Equal(domicilioDelEmisorOriginal, relectura.Emisor.Domicilio);
        Assert.Equal("Ruta 9 km 312, Rosario", relectura.Cliente.Domicilio);
    }

    /// <summary>
    /// FR-017: al rearmar la factura del mismo cliente y período, los viajes ya facturados <b>no vuelven
    /// a ofrecerse</b>. Es el valor central del módulo: ningún viaje se factura dos veces.
    /// </summary>
    [Fact]
    public async Task Los_ViajesFacturados_DejanDeOfrecerse()
    {
        var (clienteId, viajes) = await EscenarioAsync();
        var cliente = await app.CrearClienteAutenticadoAsync();
        var hoy = FechaHoyArgentina.Hoy();

        var antes = await cliente.GetFromJsonAsync<List<ViajeFacturableLeido>>(
            $"/api/facturas/facturables?clienteId={clienteId}&mes={hoy.Month}&anio={hoy.Year}");

        Assert.Equal(2, antes!.Count);

        await cliente.PostAsJsonAsync("/api/facturas", Cuerpo(clienteId, viajes.Select(v => v.Id)));

        var despues = await cliente.GetFromJsonAsync<List<ViajeFacturableLeido>>(
            $"/api/facturas/facturables?clienteId={clienteId}&mes={hoy.Month}&anio={hoy.Year}");

        Assert.Empty(despues!);
    }

    /// <summary>
    /// FR-019a: los viajes sin remito <b>aparecen igual</b> en la lista de facturables, marcados. No se
    /// esconden: quien opera tiene que ver por qué no puede facturarlos (convención [003]).
    /// </summary>
    [Fact]
    public async Task Los_ViajesSinRemito_SeOfrecenMarcados()
    {
        await app.ConfigurarEmpresaEmisoraAsync();
        var padron = await app.CrearClienteAsync();
        var hoy = FechaHoyArgentina.Hoy();

        var conRemito = await app.CrearViajeAsync(
            padron.Id,
            estado: EstadoViaje.Rendido,
            numeroRemito: ArmadoDeEscenarios.RemitoUnico(),
            importe: 10_000m);

        var sinRemito = await app.CrearViajeAsync(
            padron.Id,
            estado: EstadoViaje.Rendido,
            importe: 10_000m);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var facturables = await cliente.GetFromJsonAsync<List<ViajeFacturableLeido>>(
            $"/api/facturas/facturables?clienteId={padron.Id}&mes={hoy.Month}&anio={hoy.Year}");

        Assert.Equal(2, facturables!.Count);

        Assert.True(facturables.Single(v => v.Id == conRemito.Id).PuedeFacturarse);

        var marcado = facturables.Single(v => v.Id == sinRemito.Id);
        Assert.False(marcado.PuedeFacturarse);
        Assert.Equal("sinRemito", marcado.MotivoNoFacturable);
    }

    /// <summary>
    /// Los facturables son <b>sólo</b> los rendidos, sin facturar, con fecha en el período (FR-015 a
    /// FR-017). Los otros cuatro estados no aparecen.
    /// </summary>
    [Fact]
    public async Task Los_Facturables_ExcluyenLosOtrosEstadosYLosDeOtroPeriodo()
    {
        await app.ConfigurarEmpresaEmisoraAsync();
        var padron = await app.CrearClienteAsync();
        var hoy = FechaHoyArgentina.Hoy();

        var rendido = await app.CrearViajeAsync(
            padron.Id,
            estado: EstadoViaje.Rendido,
            numeroRemito: ArmadoDeEscenarios.RemitoUnico(),
            importe: 10_000m);

        await app.CrearViajeAsync(padron.Id, estado: EstadoViaje.Pendiente, importe: 10_000m);
        await app.CrearViajeAsync(padron.Id, estado: EstadoViaje.EnCurso, importe: 10_000m);
        await app.CrearViajeAsync(padron.Id, estado: EstadoViaje.Anulado, importe: 10_000m);

        // Rendido pero de otro mes.
        await app.CrearViajeAsync(
            padron.Id,
            fecha: hoy.AddMonths(-2),
            estado: EstadoViaje.Rendido,
            numeroRemito: ArmadoDeEscenarios.RemitoUnico(),
            importe: 10_000m);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var facturables = await cliente.GetFromJsonAsync<List<ViajeFacturableLeido>>(
            $"/api/facturas/facturables?clienteId={padron.Id}&mes={hoy.Month}&anio={hoy.Year}");

        Assert.Equal(rendido.Id, Assert.Single(facturables!).Id);
    }

    // ── Los rechazos que no crean nada ──────────────────────────────────────────────────────────

    /// <summary>
    /// FR-027: el número duplicado se rechaza <b>identificando la factura que lo usa</b>, con su fecha y
    /// su cliente. Saber que está repetido sin saber con qué no ayuda a elegir otro (convención [004]).
    /// </summary>
    [Fact]
    public async Task El_NumeroDuplicado_SeRechazaNombrandoLaFacturaQueLoUsa()
    {
        var (clienteId, viajes) = await EscenarioAsync(cantidadDeViajes: 2);
        var cliente = await app.CrearClienteAutenticadoAsync();
        var numero = DatosDePruebaFacturas.NumeroUnico();

        await cliente.PostAsJsonAsync("/api/facturas", Cuerpo(clienteId, [viajes[0].Id], numero));

        var segunda = await cliente.PostAsJsonAsync(
            "/api/facturas",
            Cuerpo(clienteId, [viajes[1].Id], numero));

        Assert.Equal(HttpStatusCode.BadRequest, segunda.StatusCode);

        var error = await segunda.Content.ReadFromJsonAsync<ErrorFacturaLeido>();
        Assert.Equal("numero_duplicado", error!.Codigo);
        Assert.Equal("numeroComprobante", error.Campo);
        Assert.NotNull(error.FacturaEnConflicto);
        Assert.Equal(numero, error.FacturaEnConflicto!.NumeroComprobante);
        Assert.Contains(numero, error.Mensaje, StringComparison.Ordinal);

        // El segundo viaje quedó intacto: el rechazo no marcó nada.
        var intacto = await app.RecargarViajeAsync(viajes[1].Id);
        Assert.Equal(EstadoViaje.Rendido, intacto!.Estado);
        Assert.Null(intacto.FacturaId);
    }

    /// <summary>FR-019a: el viaje sin remito se rechaza con <c>400</c> y sin crear nada.</summary>
    [Fact]
    public async Task El_ViajeSinRemito_SeRechazaSinCrearNada()
    {
        var (clienteId, viajes) = await EscenarioAsync(cantidadDeViajes: 1, conRemito: false);
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/facturas",
            Cuerpo(clienteId, viajes.Select(v => v.Id)));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorFacturaLeido>();
        Assert.Equal("viaje_sin_remito", error!.Codigo);

        // Nombra el viaje puntual, en el cuerpo además de en el mensaje.
        var enConflicto = Assert.Single(error.Viajes!);
        Assert.Equal(viajes[0].Id, enConflicto.Id);
        Assert.Equal("sinRemito", enConflicto.Motivo);

        Assert.Equal(0, await ContarFacturasDeAsync(clienteId));
    }

    /// <summary>
    /// FR-011a: el cliente sin domicilio se rechaza con <c>400</c> y el mensaje dice <b>dónde</b>
    /// cargarlo. El domicilio sale impreso en el bloque del cliente del documento.
    /// </summary>
    [Fact]
    public async Task El_ClienteSinDomicilio_SeRechazaDiciendoDondeCargarlo()
    {
        var (clienteId, viajes) = await EscenarioAsync(cantidadDeViajes: 1, domicilio: null);
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/facturas",
            Cuerpo(clienteId, viajes.Select(v => v.Id)));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorFacturaLeido>();
        Assert.Equal("cliente_sin_domicilio", error!.Codigo);
        Assert.Contains("padrón de clientes", error.Mensaje, StringComparison.Ordinal);

        Assert.Equal(0, await ContarFacturasDeAsync(clienteId));
    }

    /// <summary>
    /// FR-006: sin la empresa emisora configurada la emisión se rechaza <b>nombrando los datos que
    /// faltan</b>, en el cuerpo además de en el mensaje.
    /// </summary>
    [Fact]
    public async Task La_EmpresaEmisoraIncompleta_SeRechazaNombrandoLosFaltantes()
    {
        var padron = await app.CrearClienteAsync();

        await app.EnLaBaseAsync(async contexto =>
        {
            await contexto.EmpresaEmisora.ExecuteDeleteAsync();

            await contexto.Clientes
                .Where(c => c.Id == padron.Id)
                .ExecuteUpdateAsync(cambio => cambio.SetProperty(c => c.Direccion, "Ruta 9 km 312"));
        });

        var viaje = await app.CrearViajeAsync(
            padron.Id,
            estado: EstadoViaje.Rendido,
            numeroRemito: ArmadoDeEscenarios.RemitoUnico(),
            importe: 10_000m);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/facturas",
            Cuerpo(padron.Id, [viaje.Id]));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorFacturaLeido>();
        Assert.Equal("empresa_emisora_incompleta", error!.Codigo);
        Assert.Equal(["razón social", "CUIT", "domicilio", "condición de IVA"], error.Faltantes);

        Assert.Equal(0, await ContarFacturasDeAsync(padron.Id));

        // Se deja configurada para no romper el orden de los otros tests de la clase.
        await app.ConfigurarEmpresaEmisoraAsync();
    }

    /// <summary>FR-011: un cliente dado de baja no se puede facturar.</summary>
    [Fact]
    public async Task El_ClienteInactivo_SeRechaza()
    {
        var (clienteId, viajes) = await EscenarioAsync(cantidadDeViajes: 1);

        await app.EnLaBaseAsync(async contexto =>
        {
            await contexto.Clientes
                .Where(c => c.Id == clienteId)
                .ExecuteUpdateAsync(cambio => cambio.SetProperty(c => c.Activo, false));
        });

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/facturas",
            Cuerpo(clienteId, viajes.Select(v => v.Id)));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorFacturaLeido>();
        Assert.Equal("cliente_inactivo", error!.Codigo);
        Assert.Contains("Dalo de alta de nuevo", error.Mensaje, StringComparison.Ordinal);
    }

    [Fact]
    public async Task El_NumeroConFormatoInvalido_SeRechaza()
    {
        var (clienteId, viajes) = await EscenarioAsync(cantidadDeViajes: 1);
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/facturas",
            Cuerpo(clienteId, viajes.Select(v => v.Id), numero: "14-3"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorFacturaLeido>();
        Assert.Equal("numero_invalido", error!.Codigo);
        Assert.Equal("El número tiene que tener el formato 0000-00000000.", error.Mensaje);
    }

    [Fact]
    public async Task Sin_ViajesSeleccionados_SeRechaza()
    {
        var (clienteId, _) = await EscenarioAsync(cantidadDeViajes: 1);
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync("/api/facturas", Cuerpo(clienteId, []));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorFacturaLeido>();
        Assert.Equal("sin_viajes_seleccionados", error!.Codigo);
        Assert.Equal("Elegí al menos un viaje para facturar.", error.Mensaje);
    }

    /// <summary>FR-029 y FR-030: ninguno de los dos plazos puede ser anterior a la fecha de facturación.</summary>
    [Fact]
    public async Task Los_VencimientosAnterioresALaFecha_SeRechazanConSuCodigoPropio()
    {
        var (clienteId, viajes) = await EscenarioAsync(cantidadDeViajes: 1);
        var cliente = await app.CrearClienteAutenticadoAsync();
        var hoy = FechaHoyArgentina.Hoy();

        var cae = await cliente.PostAsJsonAsync(
            "/api/facturas",
            Cuerpo(clienteId, viajes.Select(v => v.Id), caeVencimiento: hoy.AddDays(-1)));

        Assert.Equal(HttpStatusCode.BadRequest, cae.StatusCode);
        Assert.Equal(
            "cae_vencimiento_anterior",
            (await cae.Content.ReadFromJsonAsync<ErrorFacturaLeido>())!.Codigo);

        var pago = await cliente.PostAsJsonAsync(
            "/api/facturas",
            Cuerpo(clienteId, viajes.Select(v => v.Id), vencimientoPago: hoy.AddDays(-1)));

        Assert.Equal(HttpStatusCode.BadRequest, pago.StatusCode);
        Assert.Equal(
            "vencimiento_pago_anterior",
            (await pago.Content.ReadFromJsonAsync<ErrorFacturaLeido>())!.Codigo);
    }

    // ── Las dos confirmaciones previas de FR-032 ────────────────────────────────────────────────

    /// <summary>
    /// FR-032: el primer intento con un viaje en cero responde <c>409</c> <b>sin crear nada</b>, y el
    /// segundo con <c>confirmado: true</c> emite. La confirmación vive en el backend porque la emisión
    /// no se deshace (research §11).
    /// </summary>
    [Fact]
    public async Task Con_UnViajeEnCero_ElPrimerIntentoNoCreaNadaYElSegundoEmite()
    {
        var (clienteId, viajes) = await EscenarioAsync(cantidadDeViajes: 1, importe: 0m);
        var cliente = await app.CrearClienteAutenticadoAsync();
        var numero = DatosDePruebaFacturas.NumeroUnico();

        var primero = await cliente.PostAsJsonAsync(
            "/api/facturas",
            Cuerpo(clienteId, viajes.Select(v => v.Id), numero));

        Assert.Equal(HttpStatusCode.Conflict, primero.StatusCode);

        var error = await primero.Content.ReadFromJsonAsync<ErrorFacturaLeido>();
        Assert.Equal("emision_requiere_confirmacion", error!.Codigo);
        Assert.Equal("viajeEnCero", error.MotivoConfirmacion);
        Assert.Equal(viajes[0].Numero, Assert.Single(error.Viajes!).Numero);

        // Nada cambió: es lo que hace válida a la confirmación previa.
        Assert.Equal(0, await ContarFacturasDeAsync(clienteId));
        Assert.Equal(EstadoViaje.Rendido, (await app.RecargarViajeAsync(viajes[0].Id))!.Estado);

        var segundo = await cliente.PostAsJsonAsync(
            "/api/facturas",
            Cuerpo(clienteId, viajes.Select(v => v.Id), numero, confirmado: true));

        Assert.Equal(HttpStatusCode.Created, segundo.StatusCode);
        Assert.Equal(EstadoViaje.Facturado, (await app.RecargarViajeAsync(viajes[0].Id))!.Estado);
    }

    /// <summary>
    /// FR-032, segunda situación: la fecha de facturación anterior a la de un viaje incluido. Suele
    /// indicar un error de carga de fechas, y el rechazo nombra el viaje y las dos fechas.
    /// </summary>
    [Fact]
    public async Task Con_LaFechaAnteriorAUnViaje_PideConfirmacionNombrandoLasDosFechas()
    {
        var hoy = FechaHoyArgentina.Hoy();

        // El viaje es de hoy y la factura de ayer: la factura es anterior al viaje que incluye.
        var (clienteId, viajes) = await EscenarioAsync(cantidadDeViajes: 1);
        var cliente = await app.CrearClienteAutenticadoAsync();
        var numero = DatosDePruebaFacturas.NumeroUnico();

        var primero = await cliente.PostAsJsonAsync(
            "/api/facturas",
            Cuerpo(clienteId, viajes.Select(v => v.Id), numero, fecha: hoy.AddDays(-1)));

        Assert.Equal(HttpStatusCode.Conflict, primero.StatusCode);

        var error = await primero.Content.ReadFromJsonAsync<ErrorFacturaLeido>();
        Assert.Equal("emision_requiere_confirmacion", error!.Codigo);
        Assert.Equal("fechaAnteriorAViaje", error.MotivoConfirmacion);
        Assert.Equal("posteriorAlaFactura", Assert.Single(error.Viajes!).Motivo);
        Assert.Contains(hoy.ToString("dd/MM/yyyy"), error.Mensaje, StringComparison.Ordinal);

        Assert.Equal(0, await ContarFacturasDeAsync(clienteId));

        var segundo = await cliente.PostAsJsonAsync(
            "/api/facturas",
            Cuerpo(clienteId, viajes.Select(v => v.Id), numero, fecha: hoy.AddDays(-1),
                confirmado: true));

        Assert.Equal(HttpStatusCode.Created, segundo.StatusCode);
    }

    // ── El documento (FR-031, SC-007a) ──────────────────────────────────────────────────────────

    /// <summary>
    /// Toda factura emitida tiene su documento: se genera en la misma operación que la crea, y el
    /// endpoint lo sirve <b>en línea</b> con un nombre que la identifica (FR-031a, SC-007a).
    /// </summary>
    [Fact]
    public async Task La_Emision_GuardaElDocumentoYSeSirveEnLinea()
    {
        var (clienteId, viajes) = await EscenarioAsync(cantidadDeViajes: 1);
        var cliente = await app.CrearClienteAutenticadoAsync();

        var emitida = await (await cliente.PostAsJsonAsync(
                "/api/facturas",
                Cuerpo(clienteId, viajes.Select(v => v.Id))))
            .Content.ReadFromJsonAsync<FacturaDetalleLeida>();

        var enLaBase = await app.RecargarFacturaAsync(emitida!.Id);
        Assert.False(string.IsNullOrWhiteSpace(enLaBase!.DocumentoRuta));
        Assert.Equal($"/api/facturas/{emitida.Id}/documento", emitida.DocumentoUrl);

        var documento = await cliente.GetAsync(emitida.DocumentoUrl);

        Assert.Equal(HttpStatusCode.OK, documento.StatusCode);
        Assert.Equal("application/pdf", documento.Content.Headers.ContentType?.MediaType);
        Assert.Equal("inline", documento.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal(["nosniff"], documento.Headers.GetValues("X-Content-Type-Options"));

        var bytes = await documento.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF"u8.ToArray(), bytes[..4]);

        Assert.Contains(
            emitida.NumeroComprobante,
            documento.Content.Headers.ContentDisposition!.ToString(),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// FR-031: <b>si el documento no se puede generar, no se crea nada y el número queda libre.</b> Es el
    /// criterio de todo o nada de FR-054, y es lo que sostiene SC-007a sin excepciones — no existe una
    /// factura emitida sin su documento.
    ///
    /// Se verifica sustituyendo el almacén por uno que falla al escribir, que es la única forma de
    /// provocar el caso sin romper nada real.
    /// </summary>
    [Fact]
    public async Task Si_ElDocumentoNoSePuedeGuardar_NoSeCreaNadaYElNumeroQuedaLibre()
    {
        var (clienteId, viajes) = await EscenarioAsync(cantidadDeViajes: 1);
        var numero = DatosDePruebaFacturas.NumeroUnico();

        // El mismo helper que usan los Módulos 3 y 4 para verificar su atomicidad: una vista de la
        // aplicación con el almacén siempre fallando, sobre la misma base.
        using var conAlmacenRoto = app.ConAlmacenQueFalla();
        var cliente = await conAlmacenRoto.CrearClienteAdministradorAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/facturas",
            Cuerpo(clienteId, viajes.Select(v => v.Id), numero));

        Assert.Equal(HttpStatusCode.InternalServerError, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorFacturaLeido>();
        Assert.Equal("archivo_no_guardado", error!.Codigo);

        // Nada quedó: ni la factura, ni el viaje marcado.
        Assert.Equal(0, await ContarFacturasDeAsync(clienteId));
        Assert.Equal(EstadoViaje.Rendido, (await app.RecargarViajeAsync(viajes[0].Id))!.Estado);

        // Y el número quedó libre: se puede emitir con el mismo, ahora sí.
        var sano = await app.CrearClienteAutenticadoAsync();

        var reintento = await sano.PostAsJsonAsync(
            "/api/facturas",
            Cuerpo(clienteId, viajes.Select(v => v.Id), numero));

        Assert.Equal(HttpStatusCode.Created, reintento.StatusCode);
    }

    /// <summary>
    /// Facturas de <b>ese cliente</b>, no de toda la tabla.
    ///
    /// Los tests de la clase comparten la base —es una <c>IClassFixture</c>—, así que contar todas las
    /// filas mediría lo que dejaron los otros tests y no lo que hizo este. Cada escenario crea su propio
    /// cliente, así que contar por cliente es lo que aísla de verdad.
    /// </summary>
    private Task<int> ContarFacturasDeAsync(int clienteId) =>
        app.ConAlcanceAsync(contexto =>
            contexto.Facturas.CountAsync(factura => factura.ClienteId == clienteId));
}
