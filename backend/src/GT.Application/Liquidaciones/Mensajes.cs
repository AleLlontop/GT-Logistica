using GT.Application.Facturacion;
using GT.Domain.Liquidaciones;

namespace GT.Application.Liquidaciones;

/// <summary>
/// Códigos de error del módulo, exactamente los de <c>contracts/liquidaciones-api.yaml</c>. El frontend
/// decide con el código y muestra el mensaje tal cual.
///
/// <b>La regla de códigos HTTP</b> (research §8, convención [005]): <c>400</c> cuando el problema está en
/// lo que se tipeó o se eligió; <c>409</c> cuando está en el estado de algo compartido o que cambió, y
/// en las dos confirmaciones pendientes.
/// </summary>
public static class CodigosErrorLiquidaciones
{
    public const string DatosInvalidos = "datos_invalidos";
    public const string PeriodoInvalido = "periodo_invalido";
    public const string EmpresaEmisoraNoConfigurada = "empresa_emisora_no_configurada";
    public const string TransportistaNoLiquidable = "transportista_no_liquidable";
    public const string SinViajes = "sin_viajes";
    public const string TotalEnCero = "total_en_cero";
    public const string ViajeNoLiquidable = "viaje_no_liquidable";
    public const string FechaDePagoFueraDeRango = "fecha_de_pago_fuera_de_rango";

    /// <summary><c>400</c> aunque dependa del saldo: el caso normal es un importe mal tipeado (research §8).</summary>
    public const string ImporteSuperaSaldo = "importe_supera_saldo";

    public const string MotivoRequerido = "motivo_requerido";
    public const string LiquidacionNoEncontrada = "liquidacion_no_encontrada";

    // ── De acá para abajo, 409 ──────────────────────────────────────────────────────────────────
    public const string ViajeYaLiquidado = "viaje_ya_liquidado";
    public const string LiquidacionNoEditable = "liquidacion_no_editable";
    public const string LiquidacionModificada = "liquidacion_modificada";
    public const string LiquidacionNoAnulable = "liquidacion_no_anulable";
    public const string LiquidacionNoPagable = "liquidacion_no_pagable";
    public const string PagoRequiereConfirmacion = "pago_requiere_confirmacion";
    public const string AnulacionRequiereConfirmacion = "anulacion_requiere_confirmacion";
}

/// <summary>
/// Textos que se muestran tal cual, en español rioplatense con voseo (Principio II). Son los que fija
/// <c>contracts/README.md</c>, con los importes en pesos y las fechas <c>dd/MM/yyyy</c> armados con el
/// mismo formateador que el documento de la factura.
/// </summary>
public static class MensajesLiquidaciones
{
    public const string DatosInvalidos = "Revisá los campos marcados.";

    public const string NoEncontrada = "No encontramos esa liquidación.";

    // ── Armado ──────────────────────────────────────────────────────────────────────────────────
    public const string TransportistaRequerido = "Elegí el transportista al que le vas a liquidar.";

    public const string MesRequerido = "Elegí el mes del período.";

    public const string AnioRequerido = "Elegí el año del período.";

    public static string PeriodoInvalido(int anioEnCurso) =>
        $"Elegí un período válido: un mes de 01 a 12 y un año de {Domain.Facturacion.PeriodoAdmitido.PrimerAnio} a {anioEnCurso}.";

    public const string EmpresaEmisoraNoConfigurada =
        "No se puede generar todavía. Falta configurar la empresa emisora: sin su CUIT el sistema no " +
        "distingue a G&T Logística de los transportistas externos. Configurala en Empresa emisora y volvé.";

    public const string TransportistaNoLiquidable =
        "Ese transportista ya no se puede liquidar: es G&T Logística o se dio de baja. Elegí otro.";

    public const string SinViajes = "Una liquidación necesita al menos un viaje.";

    public const string ViajeInexistente =
        "Alguno de los viajes elegidos no existe. Buscá los viajes de nuevo.";

    public const string TotalEnCeroAlGenerar =
        "Los viajes elegidos suman $ 0,00. No se genera una liquidación sin importe.";

    public const string TotalEnCeroAlEditar =
        "Los viajes incluidos suman $ 0,00. Una liquidación necesita importe a liquidar.";

    public static string ViajeNoLiquidable(int numeroDeViaje, MotivoViajeNoLiquidable motivo) =>
        $"El viaje #{numeroDeViaje} ya no se puede liquidar: {EnTexto(motivo)}. Buscá los viajes de nuevo.";

    public static string ViajeYaLiquidadoAlGenerar(int numeroDeViaje, string liquidacion) =>
        $"El viaje #{numeroDeViaje} ya está en la liquidación {liquidacion}. Buscá los viajes de nuevo " +
        "para ver los que siguen disponibles.";

    public static string ViajeYaLiquidadoAlEditar(int numeroDeViaje, string liquidacion) =>
        $"El viaje #{numeroDeViaje} ya está en la liquidación {liquidacion}. Volvé a abrir la edición " +
        "para ver los que siguen disponibles.";

    private static string EnTexto(MotivoViajeNoLiquidable motivo) => motivo switch
    {
        MotivoViajeNoLiquidable.NoRendidoNiFacturado => "no está rendido ni facturado",
        MotivoViajeNoLiquidable.DeOtroTransportista => "es de otro transportista",
        MotivoViajeNoLiquidable.DeOtroPeriodo => "es de otro período",
        _ => "ya está liquidado",
    };

    // ── Edición ─────────────────────────────────────────────────────────────────────────────────
    public static string LiquidacionModificada(string liquidacion) =>
        $"Otro usuario guardó cambios en la liquidación {liquidacion} mientras la editabas. Volvé a abrir " +
        "la edición para ver cómo quedó y rehacer tus cambios.";

    public static string NoEditable(string liquidacion, MotivoDeBloqueoLiquidacion motivo) => motivo switch
    {
        MotivoDeBloqueoLiquidacion.ConOrdenesDePago =>
            $"La liquidación {liquidacion} ya tiene órdenes de pago: no se puede editar.",
        MotivoDeBloqueoLiquidacion.Pagada => $"La liquidación {liquidacion} está pagada: no se puede editar.",
        MotivoDeBloqueoLiquidacion.Anulada => $"La liquidación {liquidacion} está anulada: no se puede editar.",
        _ => $"El transportista de la liquidación {liquidacion} se dio de baja o dejó de ser externo: no se " +
            "puede editar.",
    };

    // ── Anulación ───────────────────────────────────────────────────────────────────────────────
    public const string MotivoRequerido = "Escribí el motivo de la anulación: queda en el historial.";

    public const string MotivoDemasiadoLargo = "El motivo puede tener hasta 500 caracteres.";

    /// <summary>Con órdenes de pago, dice cuántas y por cuánto (FR-054, convención [004]).</summary>
    public static string NoAnulable(
        string liquidacion,
        MotivoDeBloqueoLiquidacion motivo,
        int cantidadOrdenes,
        decimal pagado) => motivo switch
    {
        MotivoDeBloqueoLiquidacion.ConOrdenesDePago =>
            $"La liquidación {liquidacion} tiene {cantidadOrdenes} " +
            $"{(cantidadOrdenes == 1 ? "orden de pago" : "órdenes de pago")} por " +
            $"{FormatoDeDocumento.Pesos(pagado)}: ya no se puede anular.",
        MotivoDeBloqueoLiquidacion.Pagada => $"La liquidación {liquidacion} está pagada: ya no se puede anular.",
        _ => $"La liquidación {liquidacion} ya está anulada.",
    };

    public static string AnulacionRequiereConfirmacion(int cantidadDeViajes) =>
        cantidadDeViajes == 1
            ? "La liquidación queda anulada y su viaje vuelve a estar disponible para liquidarse. No se " +
              "puede deshacer."
            : $"La liquidación queda anulada y sus {cantidadDeViajes} viajes vuelven a estar disponibles " +
              "para liquidarse. No se puede deshacer.";

    // ── Órdenes de pago ─────────────────────────────────────────────────────────────────────────
    public const string FechaDePagoRequerida = "Elegí la fecha en que se pagó.";

    public const string ImporteInvalido = "Escribí un importe mayor que cero. Ejemplo: 120000,00";

    public static string FechaDePagoFueraDeRango(DateOnly desde) =>
        $"La fecha de pago tiene que estar entre el {FormatoDeDocumento.Fecha(desde)} y hoy.";

    public static string ImporteSuperaSaldo(decimal restaPagar) =>
        $"El importe supera lo que resta pagar: {FormatoDeDocumento.Pesos(restaPagar)}.";

    /// <summary>La misma frase cuando el saldo lo cambió otro pago simultáneo (FR-043).</summary>
    public static string ImporteSuperaSaldoPorOtroPago(decimal restaPagar) =>
        $"{ImporteSuperaSaldo(restaPagar)} Otro pago se registró mientras tanto.";

    public static string NoPagable(string liquidacion, EstadoLiquidacion estado) =>
        estado is EstadoLiquidacion.Anulada
            ? $"La liquidación {liquidacion} ya está anulada: no admite más órdenes de pago."
            : $"La liquidación {liquidacion} ya está pagada: no admite más órdenes de pago.";

    /// <summary>
    /// FR-040. Los importes los calcula el servidor sobre el saldo actual: la pantalla los muestra tal
    /// cual (research §7).
    /// </summary>
    public static string PagoRequiereConfirmacion(decimal importe, decimal restaDespues, bool quedaPagada) =>
        $"Vas a registrar una orden de pago por {FormatoDeDocumento.Pesos(importe)}. " +
        (quedaPagada
            ? "Con ella la liquidación queda pagada y se cierra."
            : $"Después de ella resta pagar {FormatoDeDocumento.Pesos(restaDespues)}.") +
        " Una orden de pago no se modifica ni se elimina.";
}
