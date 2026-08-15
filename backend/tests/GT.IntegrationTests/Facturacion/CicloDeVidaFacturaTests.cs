using System.Net;
using System.Net.Http.Json;
using GT.Domain.Choferes;
using GT.Domain.Facturacion;
using GT.Domain.Viajes;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Viajes;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Facturacion;

/// <summary>
/// El ciclo de vida completo de la factura: corrección, cobro, panel de vencimientos, anulación y
/// refacturación (User Stories 4, 5 y 6).
///
/// Va todo junto porque las cuatro operaciones comparten el mismo escenario —una factura emitida por el
/// endpoint real, con sus viajes— y armarlo por separado en cuatro archivos multiplicaría el mismo
/// preámbulo sin agregar nada.
/// </summary>
public class CicloDeVidaFacturaTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    /// <summary>
    /// Una factura emitida <b>por el endpoint real</b>, con la cantidad de viajes que el test pida.
    /// Marcarla a mano en la base saltearía justo lo que interesa verificar.
    /// </summary>
    private async Task<(FacturaDetalleLeida Factura, int ClienteId, Viaje[] Viajes)> EmitirAsync(
        HttpClient cliente,
        int cantidadDeViajes = 2,
        DateOnly? fecha = null,
        DateOnly? vencimientoPago = null)
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
                numeroRemito: ArmadoDeEscenarios.RemitoUnico(),
                importe: 50_000m));
        }

        var hoy = fecha ?? FechaHoyArgentina.Hoy();

        var respuesta = await cliente.PostAsJsonAsync("/api/facturas", new
        {
            clienteId = padron.Id,
            tipoComprobante = "facturaA",
            tipoFacturacion = "original",
            condicionDeVenta = "cuentaCorriente",
            mes = hoy.Month,
            anio = hoy.Year,
            fecha = hoy.ToString("yyyy-MM-dd"),
            numeroComprobante = DatosDePruebaFacturas.NumeroUnico(),
            detalle = "Servicios del período.",
            cae = "75123456789012",
            caeVencimiento = hoy.AddDays(10).ToString("yyyy-MM-dd"),
            vencimientoPago = (vencimientoPago ?? hoy.AddDays(30)).ToString("yyyy-MM-dd"),
            viajeIds = viajes.Select(v => v.Id).ToArray(),
        });

        respuesta.EnsureSuccessStatusCode();

        var factura = (await respuesta.Content.ReadFromJsonAsync<FacturaDetalleLeida>())!;

        return (factura, padron.Id, [.. viajes]);
    }

    // ── User Story 4: corrección (FR-035 a FR-038, FR-031b) ─────────────────────────────────────

    /// <summary>
    /// Se corrigen los cuatro campos, <b>el documento se regenera y el anterior no queda</b>, y se agrega
    /// una entrada de corrección al historial con <c>EstadoNuevo = null</c> (FR-035, FR-031b, FR-037).
    /// </summary>
    [Fact]
    public async Task La_Correccion_CambiaLosCuatroCamposYRegeneraElDocumento()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var (factura, _, _) = await EmitirAsync(cliente);

        var antes = await app.RecargarFacturaAsync(factura.Id);
        var rutaAnterior = antes!.DocumentoRuta;

        var hoy = FechaHoyArgentina.Hoy();

        var respuesta = await cliente.PutAsJsonAsync($"/api/facturas/{factura.Id}", new
        {
            detalle = "Detalle corregido.",
            cae = "99999999999999",
            caeVencimiento = hoy.AddDays(20).ToString("yyyy-MM-dd"),
            vencimientoPago = hoy.AddDays(45).ToString("yyyy-MM-dd"),
        });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var corregida = await respuesta.Content.ReadFromJsonAsync<FacturaDetalleLeida>();

        Assert.Equal("Detalle corregido.", corregida!.Detalle);
        Assert.Equal("99999999999999", corregida.Cae);
        Assert.Equal(hoy.AddDays(20).ToString("yyyy-MM-dd"), corregida.CaeVencimiento);
        Assert.Equal(hoy.AddDays(45).ToString("yyyy-MM-dd"), corregida.VencimientoPago);

        // El documento se regeneró como archivo **nuevo** y el anterior ya no está (research §6).
        var despues = await app.RecargarFacturaAsync(factura.Id);
        Assert.NotEqual(rutaAnterior, despues!.DocumentoRuta);

        // Y el documento nuevo trae el CAE corregido: es la razón de ser de FR-031b.
        var documento = await cliente.GetAsync(corregida.DocumentoUrl);
        Assert.Equal(HttpStatusCode.OK, documento.StatusCode);

        // Una entrada de corrección: `EstadoNuevo` en nulo es la marca (FR-037).
        var historial = await app.HistorialDeFacturaAsync(factura.Id);
        var correccion = Assert.Single(historial, entrada => entrada.EstadoNuevo is null);

        Assert.Null(correccion.EstadoAnterior);
        Assert.True(correccion.EsCorreccion);
    }

    /// <summary>
    /// FR-036: intentar cambiar el cliente, los viajes o los importes <b>no tiene efecto aunque se invoque
    /// la acción directamente</b> — esos campos no están en el contrato de entrada (SC-013).
    /// </summary>
    [Fact]
    public async Task La_Correccion_NoPuedeCambiarClienteViajesNiImportes()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var (factura, _, _) = await EmitirAsync(cliente);
        var otroCliente = await app.CrearClienteAsync();

        var hoy = FechaHoyArgentina.Hoy();

        // Se mandan de más, a mano: el endpoint los ignora porque no existen en `CorreccionRequest`.
        var respuesta = await cliente.PutAsJsonAsync($"/api/facturas/{factura.Id}", new
        {
            cae = factura.Cae,
            caeVencimiento = factura.CaeVencimiento,
            vencimientoPago = factura.VencimientoPago,
            clienteId = otroCliente.Id,
            viajeIds = Array.Empty<int>(),
            neto = 1m,
            iva = 1m,
            total = 2m,
            estado = "pagada",
            fechaCobro = hoy.ToString("yyyy-MM-dd"),
        });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var despues = await app.RecargarFacturaAsync(factura.Id);

        Assert.NotEqual(otroCliente.Id, despues!.ClienteId);
        Assert.Equal(100_000m, despues.Neto);
        Assert.Equal(121_000m, despues.Total);

        // FR-044 y research §15.5: el `PUT` **no puede tocar el estado ni la fecha de cobro**.
        Assert.Equal(EstadoFactura.Pendiente, despues.Estado);
        Assert.Null(despues.FechaCobro);

        // Y los viajes siguen todos en la factura.
        var cantidad = await app.ConAlcanceAsync(contexto =>
            contexto.Viajes.CountAsync(viaje => viaje.FacturaId == factura.Id));

        Assert.Equal(2, cantidad);
    }

    /// <summary>US4 esc. 6: una factura emitida no puede quedarse sin CAE ni sin su vencimiento.</summary>
    [Fact]
    public async Task La_Correccion_RechazaVaciarElCaeOSuVencimiento()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var (factura, _, _) = await EmitirAsync(cliente, cantidadDeViajes: 1);

        var sinCae = await cliente.PutAsJsonAsync($"/api/facturas/{factura.Id}", new
        {
            cae = "   ",
            caeVencimiento = factura.CaeVencimiento,
            vencimientoPago = factura.VencimientoPago,
        });

        Assert.Equal(HttpStatusCode.BadRequest, sinCae.StatusCode);

        var error = await sinCae.Content.ReadFromJsonAsync<ErrorFacturaLeido>();
        Assert.Equal("cae_requerido", error!.Codigo);
        Assert.Equal("Una factura emitida no puede quedarse sin CAE.", error.Mensaje);

        var sinVencimiento = await cliente.PutAsJsonAsync($"/api/facturas/{factura.Id}", new
        {
            cae = factura.Cae,
            caeVencimiento = (string?)null,
            vencimientoPago = factura.VencimientoPago,
        });

        Assert.Equal(HttpStatusCode.BadRequest, sinVencimiento.StatusCode);
        Assert.Equal(
            "Una factura emitida no puede quedarse sin vencimiento del CAE.",
            (await sinVencimiento.Content.ReadFromJsonAsync<ErrorFacturaLeido>())!.Mensaje);
    }

    /// <summary>
    /// US4 esc. 8: corregir una factura <c>pagada</c> está permitido y <b>no le toca el estado ni la fecha
    /// de cobro</b>.
    /// </summary>
    [Fact]
    public async Task La_Correccion_DeUnaPagada_NoLeTocaElEstadoNiLaFechaDeCobro()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var (factura, _, _) = await EmitirAsync(cliente, cantidadDeViajes: 1);
        var hoy = FechaHoyArgentina.Hoy();

        var cobro = await cliente.PostAsJsonAsync(
            $"/api/facturas/{factura.Id}/cobro",
            new { fechaCobro = hoy.ToString("yyyy-MM-dd") });

        Assert.Equal(HttpStatusCode.OK, cobro.StatusCode);

        var correccion = await cliente.PutAsJsonAsync($"/api/facturas/{factura.Id}", new
        {
            detalle = "Corregido después de cobrar.",
            cae = "88888888888888",
            caeVencimiento = factura.CaeVencimiento,
            vencimientoPago = factura.VencimientoPago,
        });

        Assert.Equal(HttpStatusCode.OK, correccion.StatusCode);

        var despues = await app.RecargarFacturaAsync(factura.Id);

        Assert.Equal(EstadoFactura.Pagada, despues!.Estado);
        Assert.Equal(hoy, despues.FechaCobro);
        Assert.Equal("88888888888888", despues.Cae);
    }

    /// <summary>FR-038: una factura anulada rechaza la corrección. Es el único estado que la cierra.</summary>
    [Fact]
    public async Task La_Correccion_DeUnaAnulada_SeRechaza()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var (factura, _, _) = await EmitirAsync(cliente, cantidadDeViajes: 1);

        await cliente.PostAsJsonAsync(
            $"/api/facturas/{factura.Id}/anulacion",
            new { motivo = "Cliente equivocado." });

        var respuesta = await cliente.PutAsJsonAsync($"/api/facturas/{factura.Id}", new
        {
            cae = factura.Cae,
            caeVencimiento = factura.CaeVencimiento,
            vencimientoPago = factura.VencimientoPago,
        });

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorFacturaLeido>();
        Assert.Equal("factura_anulada_inmutable", error!.Codigo);
        Assert.Equal("Una factura anulada no se puede corregir.", error.Mensaje);
    }

    /// <summary>
    /// <b>La respuesta de las tres escrituras trae el historial completo</b>, no sólo la línea que acaba de
    /// escribirse.
    ///
    /// Lo encontró el recorrido manual del <c>quickstart.md</c>: los tres casos de uso mapeaban la entidad
    /// con la que habían escrito, que viene de <c>ObtenerParaModificarAsync</c> y no trae el historial. La
    /// pantalla reemplaza su estado con lo que llega, así que el historial se veía con una sola entrada
    /// hasta que alguien recargaba. Los tres releen la ficha, igual que la emisión.
    /// </summary>
    [Fact]
    public async Task Las_TresEscrituras_DevuelvenElHistorialCompleto()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var (factura, _, _) = await EmitirAsync(cliente, cantidadDeViajes: 1);
        var hoy = FechaHoyArgentina.Hoy();

        // La emisión ya dejó una entrada.
        Assert.Single(factura.Historial);

        // Corregir: la respuesta trae la emisión **y** la corrección.
        var corregida = await (await cliente.PutAsJsonAsync($"/api/facturas/{factura.Id}", new
        {
            detalle = "Corregido.",
            cae = factura.Cae,
            caeVencimiento = factura.CaeVencimiento,
            vencimientoPago = factura.VencimientoPago,
        })).Content.ReadFromJsonAsync<FacturaDetalleLeida>();

        Assert.Equal(2, corregida!.Historial.Count);
        Assert.Contains(corregida.Historial, entrada => entrada.EstadoNuevo == "pendiente");
        Assert.Contains(corregida.Historial, entrada => entrada.EstadoNuevo is null);

        // Y el usuario de cada línea viene resuelto, que es lo que la pantalla muestra.
        Assert.All(corregida.Historial, entrada => Assert.Equal("admin", entrada.Usuario));

        // Cobrar: las tres.
        var cobrada = await (await cliente.PostAsJsonAsync(
                $"/api/facturas/{factura.Id}/cobro",
                new { fechaCobro = hoy.ToString("yyyy-MM-dd") }))
            .Content.ReadFromJsonAsync<FacturaDetalleLeida>();

        Assert.Equal(3, cobrada!.Historial.Count);
        Assert.Contains(cobrada.Historial, entrada => entrada.EstadoNuevo == "pagada");
    }

    /// <summary>
    /// Y la respuesta de la anulación también, con una consecuencia que conviene tener escrita: <b>la ficha
    /// de una anulada viene sin viajes</b>, porque la anulación les puso <c>FacturaId</c> en nulo y volvieron
    /// a <c>rendido</c> (data-model §Anular).
    ///
    /// No es una pérdida de información silenciosa: el detalle de qué viajes tenía quedó impreso en el
    /// documento regenerado, y la pantalla lo explica con palabras en vez de mostrar una tabla vacía.
    /// </summary>
    [Fact]
    public async Task La_FichaDeUnaAnulada_VieneSinViajesYConSuHistorialCompleto()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var (factura, _, _) = await EmitirAsync(cliente, cantidadDeViajes: 3);

        Assert.Equal(3, factura.Viajes.Count);

        var anulada = await (await cliente.PostAsJsonAsync(
                $"/api/facturas/{factura.Id}/anulacion",
                new { motivo = "Cliente equivocado." }))
            .Content.ReadFromJsonAsync<FacturaDetalleLeida>();

        Assert.Equal("anulada", anulada!.Estado);
        Assert.Empty(anulada.Viajes);
        Assert.Equal(2, anulada.Historial.Count);

        // Y el `GET` posterior dice lo mismo: la respuesta de la escritura y la relectura coinciden.
        var relectura = await cliente.GetFromJsonAsync<FacturaDetalleLeida>(
            $"/api/facturas/{factura.Id}");

        Assert.Empty(relectura!.Viajes);
        Assert.Equal(anulada.Historial.Count, relectura.Historial.Count);
    }

    // ── User Story 5: cobro y vencimientos (FR-042, FR-043, FR-063) ─────────────────────────────

    [Fact]
    public async Task El_Cobro_DejaLaFacturaPagadaConSuFechaYSuHistorial()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var (factura, _, _) = await EmitirAsync(cliente, cantidadDeViajes: 1);
        var hoy = FechaHoyArgentina.Hoy();

        var respuesta = await cliente.PostAsJsonAsync(
            $"/api/facturas/{factura.Id}/cobro",
            new { fechaCobro = hoy.ToString("yyyy-MM-dd") });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var cobrada = await respuesta.Content.ReadFromJsonAsync<FacturaDetalleLeida>();
        Assert.Equal("pagada", cobrada!.Estado);
        Assert.Equal(hoy.ToString("yyyy-MM-dd"), cobrada.FechaCobro);

        var historial = await app.HistorialDeFacturaAsync(factura.Id);
        Assert.Contains(
            historial,
            entrada => entrada.EstadoAnterior == EstadoFactura.Pendiente &&
                entrada.EstadoNuevo == EstadoFactura.Pagada);
    }

    /// <summary>FR-042: la fecha de cobro no puede ser anterior a la de facturación.</summary>
    [Fact]
    public async Task El_Cobro_ConFechaAnteriorALaFacturacion_SeRechaza()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var (factura, _, _) = await EmitirAsync(cliente, cantidadDeViajes: 1);
        var hoy = FechaHoyArgentina.Hoy();

        var respuesta = await cliente.PostAsJsonAsync(
            $"/api/facturas/{factura.Id}/cobro",
            new { fechaCobro = hoy.AddDays(-1).ToString("yyyy-MM-dd") });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorFacturaLeido>();
        Assert.Equal("fecha_cobro_anterior", error!.Codigo);
        Assert.Equal(
            "La fecha de cobro no puede ser anterior a la fecha de facturación.",
            error.Mensaje);
    }

    /// <summary>
    /// <c>pagada</c> es terminal: cobrar de nuevo se rechaza, y una anulada no admite cobro. <b>No existe
    /// ningún endpoint que revierta el cobro</b> (FR-043).
    /// </summary>
    [Fact]
    public async Task Una_PagadaOAnulada_NoAdmiteCobro()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var hoy = FechaHoyArgentina.Hoy();

        var (pagada, _, _) = await EmitirAsync(cliente, cantidadDeViajes: 1);
        await cliente.PostAsJsonAsync(
            $"/api/facturas/{pagada.Id}/cobro",
            new { fechaCobro = hoy.ToString("yyyy-MM-dd") });

        var segundoCobro = await cliente.PostAsJsonAsync(
            $"/api/facturas/{pagada.Id}/cobro",
            new { fechaCobro = hoy.ToString("yyyy-MM-dd") });

        Assert.Equal(HttpStatusCode.Conflict, segundoCobro.StatusCode);
        Assert.Equal(
            "transicion_no_permitida",
            (await segundoCobro.Content.ReadFromJsonAsync<ErrorFacturaLeido>())!.Codigo);

        var (anulada, _, _) = await EmitirAsync(cliente, cantidadDeViajes: 1);
        await cliente.PostAsJsonAsync(
            $"/api/facturas/{anulada.Id}/anulacion",
            new { motivo = "Error de carga." });

        var cobroDeAnulada = await cliente.PostAsJsonAsync(
            $"/api/facturas/{anulada.Id}/cobro",
            new { fechaCobro = hoy.ToString("yyyy-MM-dd") });

        Assert.Equal(HttpStatusCode.Conflict, cobroDeAnulada.StatusCode);
    }

    /// <summary>
    /// FR-063: el panel devuelve las <c>vencida</c> y las que vencen dentro de los <b>7 días corridos</b>,
    /// y <b>excluye</b> las <c>pagada</c> y las <c>anulada</c>.
    /// </summary>
    [Fact]
    public async Task El_PanelDeVencimientos_TraeLoVencidoYLoProximoYExcluyeLoCerrado()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();
        var hoy = FechaHoyArgentina.Hoy();

        var vencida = await app.CrearFacturaAsync(
            padron.Id, fecha: hoy.AddDays(-40), vencimientoPago: hoy.AddDays(-5));

        var venceHoy = await app.CrearFacturaAsync(
            padron.Id, fecha: hoy.AddDays(-30), vencimientoPago: hoy);

        var dentroDeLaVentana = await app.CrearFacturaAsync(
            padron.Id, fecha: hoy, vencimientoPago: hoy.AddDays(7));

        var fueraDeLaVentana = await app.CrearFacturaAsync(
            padron.Id, fecha: hoy, vencimientoPago: hoy.AddDays(8));

        var cobrada = await app.CrearFacturaAsync(
            padron.Id,
            estado: EstadoFactura.Pagada,
            fecha: hoy.AddDays(-40),
            vencimientoPago: hoy.AddDays(-5),
            fechaCobro: hoy.AddDays(-2));

        var anuladaVencida = await app.CrearFacturaAsync(
            padron.Id,
            estado: EstadoFactura.Anulada,
            fecha: hoy.AddDays(-40),
            vencimientoPago: hoy.AddDays(-5),
            motivoAnulacion: "Error.");

        var panel = await cliente.GetFromJsonAsync<List<FilaDeVencimientoLeida>>(
            "/api/facturas/vencimientos");

        var ids = panel!.Select(fila => fila.Id).ToHashSet();

        Assert.Contains(vencida.Id, ids);
        Assert.Contains(venceHoy.Id, ids);
        Assert.Contains(dentroDeLaVentana.Id, ids);

        Assert.DoesNotContain(fueraDeLaVentana.Id, ids);
        Assert.DoesNotContain(cobrada.Id, ids);
        Assert.DoesNotContain(anuladaVencida.Id, ids);

        // Los días: negativo es atraso, cero es "vence hoy" (FR-063).
        Assert.Equal(-5, panel.Single(fila => fila.Id == vencida.Id).Dias);
        Assert.Equal(0, panel.Single(fila => fila.Id == venceHoy.Id).Dias);
        Assert.Equal(7, panel.Single(fila => fila.Id == dentroDeLaVentana.Id).Dias);
    }

    // ── User Story 6: anulación y refacturación (FR-046 a FR-050) ──────────────────────────────

    /// <summary>
    /// La anulación deja la factura <c>anulada</c> con su motivo, escribe el historial, <b>devuelve todos
    /// los viajes a <c>rendido</c></b> con su <c>FacturaId</c> en nulo y una línea de
    /// <c>CambioDeEstadoViaje</c> por viaje (FR-046 a FR-048).
    /// </summary>
    [Fact]
    public async Task La_Anulacion_DevuelveTodosLosViajesARendido()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var (factura, clienteId, viajes) = await EmitirAsync(cliente, cantidadDeViajes: 3);

        var antes = await app.RecargarFacturaAsync(factura.Id);
        var rutaAnterior = antes!.DocumentoRuta;

        var respuesta = await cliente.PostAsJsonAsync(
            $"/api/facturas/{factura.Id}/anulacion",
            new { motivo = "Se facturó al cliente equivocado." });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var anulada = await respuesta.Content.ReadFromJsonAsync<FacturaDetalleLeida>();
        Assert.Equal("anulada", anulada!.Estado);
        Assert.Equal("Se facturó al cliente equivocado.", anulada.MotivoAnulacion);

        // **Todos** los viajes, no algunos (FR-048).
        foreach (var viaje in viajes)
        {
            var despues = await app.RecargarViajeAsync(viaje.Id);

            Assert.Equal(EstadoViaje.Rendido, despues!.Estado);
            Assert.Null(despues.FacturaId);

            var historialDelViaje = await app.HistorialDeAsync(viaje.Id);
            Assert.Contains(
                historialDelViaje,
                linea => linea.EstadoAnterior == EstadoViaje.Facturado &&
                    linea.EstadoNuevo == EstadoViaje.Rendido);
        }

        // El documento se regeneró **dentro** de la transacción y el anterior no quedó (FR-031b).
        var facturaDespues = await app.RecargarFacturaAsync(factura.Id);
        Assert.NotEqual(rutaAnterior, facturaDespues!.DocumentoRuta);

        // Y los viajes vuelven a ofrecerse para facturar (US6).
        var hoy = FechaHoyArgentina.Hoy();
        var facturables = await cliente.GetFromJsonAsync<List<ViajeFacturableLeido>>(
            $"/api/facturas/facturables?clienteId={clienteId}&mes={hoy.Month}&anio={hoy.Year}");

        Assert.Equal(3, facturables!.Count);

        var historial = await app.HistorialDeFacturaAsync(factura.Id);
        Assert.Contains(
            historial,
            entrada => entrada.EstadoAnterior == EstadoFactura.Pendiente &&
                entrada.EstadoNuevo == EstadoFactura.Anulada);
    }

    /// <summary>FR-046: sin motivo escrito la anulación se rechaza y nada cambia.</summary>
    [Fact]
    public async Task La_Anulacion_SinMotivo_SeRechaza()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var (factura, _, viajes) = await EmitirAsync(cliente, cantidadDeViajes: 1);

        var respuesta = await cliente.PostAsJsonAsync(
            $"/api/facturas/{factura.Id}/anulacion",
            new { motivo = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorFacturaLeido>();
        Assert.Equal("motivo_requerido", error!.Codigo);

        // Nada cambió: ni la factura ni el viaje.
        var despues = await app.RecargarFacturaAsync(factura.Id);
        Assert.Equal(EstadoFactura.Pendiente, despues!.Estado);
        Assert.Equal(EstadoViaje.Facturado, (await app.RecargarViajeAsync(viajes[0].Id))!.Estado);
    }

    /// <summary>
    /// FR-043a: anular una <c>pagada</c> responde <c>409</c> <b>informando desde qué fecha está
    /// cobrada</b>, y sin ofrecer ni sugerir revertir el cobro.
    /// </summary>
    [Fact]
    public async Task La_Anulacion_DeUnaCobrada_InformaDesdeCuandoLoEsta()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var (factura, _, _) = await EmitirAsync(cliente, cantidadDeViajes: 1);
        var hoy = FechaHoyArgentina.Hoy();

        await cliente.PostAsJsonAsync(
            $"/api/facturas/{factura.Id}/cobro",
            new { fechaCobro = hoy.ToString("yyyy-MM-dd") });

        var respuesta = await cliente.PostAsJsonAsync(
            $"/api/facturas/{factura.Id}/anulacion",
            new { motivo = "Ya no sirve." });

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorFacturaLeido>();

        Assert.Equal("factura_cobrada", error!.Codigo);
        Assert.Contains(hoy.ToString("dd/MM/yyyy"), error.Mensaje, StringComparison.Ordinal);
        Assert.Equal(hoy.ToString("yyyy-MM-dd"), error.FechaCobro);

        // El mensaje **no** sugiere revertir el cobro: esa acción no existe.
        Assert.DoesNotContain("revert", error.Mensaje, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// FR-049: la Refacturación referencia a la anulada, y <b>las dos fichas se muestran una a la otra</b>
    /// (FR-050).
    /// </summary>
    [Fact]
    public async Task La_Refacturacion_ReferenciaALaAnuladaEnLasDosDirecciones()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var (original, clienteId, viajes) = await EmitirAsync(cliente, cantidadDeViajes: 2);

        await cliente.PostAsJsonAsync(
            $"/api/facturas/{original.Id}/anulacion",
            new { motivo = "Importes mal cargados." });

        // El desplegable ofrece exactamente esa anulada (FR-049).
        var ofrecidas = await cliente.GetFromJsonAsync<List<FacturaResumenLeido>>(
            $"/api/facturas/anuladas-sin-reemplazo?clienteId={clienteId}");

        Assert.Contains(ofrecidas!, resumen => resumen.Id == original.Id);

        var hoy = FechaHoyArgentina.Hoy();

        var respuesta = await cliente.PostAsJsonAsync("/api/facturas", new
        {
            clienteId,
            tipoComprobante = "facturaA",
            tipoFacturacion = "refacturacion",
            condicionDeVenta = "cuentaCorriente",
            mes = hoy.Month,
            anio = hoy.Year,
            fecha = hoy.ToString("yyyy-MM-dd"),
            numeroComprobante = DatosDePruebaFacturas.NumeroUnico(),
            cae = "75123456789012",
            caeVencimiento = hoy.AddDays(10).ToString("yyyy-MM-dd"),
            vencimientoPago = hoy.AddDays(30).ToString("yyyy-MM-dd"),
            facturaReemplazadaId = original.Id,
            viajeIds = viajes.Select(v => v.Id).ToArray(),
        });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var refacturacion = await respuesta.Content.ReadFromJsonAsync<FacturaDetalleLeida>();

        // Dirección 1: la nueva dice a cuál reemplaza.
        Assert.NotNull(refacturacion!.ReemplazaA);
        Assert.Equal(original.Id, refacturacion.ReemplazaA!.Id);

        // Dirección 2: la anulada dice cuál la reemplazó, **resuelta por consulta** (FR-050).
        var anuladaRelectura = await cliente.GetFromJsonAsync<FacturaDetalleLeida>(
            $"/api/facturas/{original.Id}");

        Assert.NotNull(anuladaRelectura!.ReemplazadaPor);
        Assert.Equal(refacturacion.Id, anuladaRelectura.ReemplazadaPor!.Id);

        // Y ya no se ofrece para refacturar de nuevo (FR-049a).
        var despues = await cliente.GetFromJsonAsync<List<FacturaResumenLeido>>(
            $"/api/facturas/anuladas-sin-reemplazo?clienteId={clienteId}");

        Assert.DoesNotContain(despues!, resumen => resumen.Id == original.Id);
    }

    /// <summary>
    /// FR-049: <c>Refacturación</c> sin factura reemplazada se rechaza, y <c>Original</c> con referencia
    /// también. Ignorar el campo guardaría una factura distinta de la que se pidió.
    /// </summary>
    [Fact]
    public async Task La_Refacturacion_ExigeLaReferenciaYLaOriginalLaProhibe()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var (original, clienteId, viajes) = await EmitirAsync(cliente, cantidadDeViajes: 2);

        await cliente.PostAsJsonAsync(
            $"/api/facturas/{original.Id}/anulacion",
            new { motivo = "Error." });

        var hoy = FechaHoyArgentina.Hoy();

        object Cuerpo(string tipoFacturacion, int? reemplazadaId) => new
        {
            clienteId,
            tipoComprobante = "facturaA",
            tipoFacturacion,
            condicionDeVenta = "cuentaCorriente",
            mes = hoy.Month,
            anio = hoy.Year,
            fecha = hoy.ToString("yyyy-MM-dd"),
            numeroComprobante = DatosDePruebaFacturas.NumeroUnico(),
            cae = "75123456789012",
            caeVencimiento = hoy.AddDays(10).ToString("yyyy-MM-dd"),
            vencimientoPago = hoy.AddDays(30).ToString("yyyy-MM-dd"),
            facturaReemplazadaId = reemplazadaId,
            viajeIds = viajes.Select(v => v.Id).ToArray(),
        };

        var sinReferencia = await cliente.PostAsJsonAsync(
            "/api/facturas",
            Cuerpo("refacturacion", null));

        Assert.Equal(HttpStatusCode.BadRequest, sinReferencia.StatusCode);
        Assert.Equal(
            "refacturacion_sin_reemplazada",
            (await sinReferencia.Content.ReadFromJsonAsync<ErrorFacturaLeido>())!.Codigo);

        var originalConReferencia = await cliente.PostAsJsonAsync(
            "/api/facturas",
            Cuerpo("original", original.Id));

        Assert.Equal(HttpStatusCode.BadRequest, originalConReferencia.StatusCode);
        Assert.Equal(
            "original_con_reemplazada",
            (await originalConReferencia.Content.ReadFromJsonAsync<ErrorFacturaLeido>())!.Codigo);
    }

    /// <summary>
    /// FR-049a: una anulada ya reemplazada responde <c>409</c> <b>nombrando la Refacturación que la
    /// reemplaza</b>.
    /// </summary>
    [Fact]
    public async Task Una_AnuladaYaReemplazada_SeRechazaNombrandoLaQueLaReemplaza()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var (original, clienteId, viajes) = await EmitirAsync(cliente, cantidadDeViajes: 2);

        await cliente.PostAsJsonAsync(
            $"/api/facturas/{original.Id}/anulacion",
            new { motivo = "Error." });

        var hoy = FechaHoyArgentina.Hoy();

        object Cuerpo() => new
        {
            clienteId,
            tipoComprobante = "facturaA",
            tipoFacturacion = "refacturacion",
            condicionDeVenta = "cuentaCorriente",
            mes = hoy.Month,
            anio = hoy.Year,
            fecha = hoy.ToString("yyyy-MM-dd"),
            numeroComprobante = DatosDePruebaFacturas.NumeroUnico(),
            cae = "75123456789012",
            caeVencimiento = hoy.AddDays(10).ToString("yyyy-MM-dd"),
            vencimientoPago = hoy.AddDays(30).ToString("yyyy-MM-dd"),
            facturaReemplazadaId = original.Id,
            viajeIds = viajes.Select(v => v.Id).ToArray(),
        };

        var primera = await cliente.PostAsJsonAsync("/api/facturas", Cuerpo());
        Assert.Equal(HttpStatusCode.Created, primera.StatusCode);

        var refacturacion = await primera.Content.ReadFromJsonAsync<FacturaDetalleLeida>();

        // Se anula la Refacturación para liberar los viajes y poder intentar la segunda.
        await cliente.PostAsJsonAsync(
            $"/api/facturas/{refacturacion!.Id}/anulacion",
            new { motivo = "Se anula para probar el rechazo." });

        var segunda = await cliente.PostAsJsonAsync("/api/facturas", Cuerpo());

        Assert.Equal(HttpStatusCode.Conflict, segunda.StatusCode);

        var error = await segunda.Content.ReadFromJsonAsync<ErrorFacturaLeido>();

        Assert.Equal("anulada_ya_reemplazada", error!.Codigo);
        Assert.NotNull(error.FacturaEnConflicto);
        Assert.Equal(refacturacion.Id, error.FacturaEnConflicto!.Id);
        Assert.Contains(
            refacturacion.NumeroComprobante,
            error.Mensaje,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// FR-031d: el documento de una anulada se sirve <b>en cualquier estado</b> y ya trae impresas la
    /// leyenda y el motivo, porque se regeneró al anularla — no se estampa al servir el archivo.
    /// </summary>
    [Fact]
    public async Task El_DocumentoDeUnaAnulada_SeSirveIgual()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var (factura, _, _) = await EmitirAsync(cliente, cantidadDeViajes: 1);

        await cliente.PostAsJsonAsync(
            $"/api/facturas/{factura.Id}/anulacion",
            new { motivo = "Cliente equivocado." });

        var documento = await cliente.GetAsync($"/api/facturas/{factura.Id}/documento");

        Assert.Equal(HttpStatusCode.OK, documento.StatusCode);
        Assert.Equal("application/pdf", documento.Content.Headers.ContentType?.MediaType);

        var bytes = await documento.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF"u8.ToArray(), bytes[..4]);
    }
}
