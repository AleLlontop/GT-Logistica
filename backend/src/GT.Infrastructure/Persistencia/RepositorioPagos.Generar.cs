using GT.Application.Pagos;
using GT.Domain.Caja;
using GT.Domain.Choferes;
using GT.Domain.Liquidaciones;
using GT.Domain.Pagos;
using GT.Domain.Proveedores;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

public partial class RepositorioPagos
{
    public async Task<ResultadoEscrituraPago> GenerarAsync(GenerarOrdenEntrada entrada, int usuarioId, bool puedeConsultar,
        DateOnly hoy, DateTime ahoraUtc, CancellationToken ct = default)
    {
        if (!entrada.Confirmado) throw new ReglaPagoException("confirmacion_requerida", "Confirmá explícitamente Generar.", campo: "confirmado");
        if (entrada.ClaveOperacion == Guid.Empty || entrada.ProveedorId <= 0 || entrada.FacturaIds is not { Length: > 0 }
            || entrada.Medios is not { Length: > 0 } || entrada.Medios.Any(x => x is null)
            || entrada.FacturaIds.Any(x => x <= 0) || entrada.FacturaIds.Distinct().Count() != entrada.FacturaIds.Length)
            throw new ReglaPagoException("intencion_invalida", "Revisá proveedor, facturas, medios y clave de operación.");
        if (!ReglasDePagos.IntentarSumarImportes(entrada.Medios.Select(x => x.Importe), out _))
            throw new ReglaPagoException("medios_invalidos", "Los importes deben ser positivos, exactos y estar dentro de capacidad.", campo: "medios");
        var comando = CanonicalizadorPago.Generar(entrada);
        var commitIntentado = false;
        try
        {
            await using var tx = await contexto.Database.BeginTransactionAsync(ct);
            var locks = new BloqueosPagos(contexto);
            var resultados = new ResultadosIdempotentesPago(contexto);
            await locks.IdempotenciaAsync(TipoOperacionPago.Generar, entrada.ClaveOperacion, ct);
            var replay = await resultados.BuscarAsync(TipoOperacionPago.Generar, entrada.ClaveOperacion, comando, usuarioId, puedeConsultar, ct);
            if (replay is not null) return replay;
            if (!ReglasDePagos.FechaOrdenValida(entrada.Fecha, hoy))
                throw new ReglaPagoException("fecha_invalida", "La fecha es obligatoria y no puede ser futura.", campo: "fecha");
            await locks.BancosAsync(entrada.Medios.OfType<ChequeEntrada>().Select(x => x.BancoId), ct);
            await locks.FacturasAsync(entrada.FacturaIds, true, ct);
            var facturas = await contexto.FacturasProveedor.AsNoTracking().Where(x => entrada.FacturaIds.Contains(x.Id)).OrderBy(x => x.Id).ToListAsync(ct);
            if (facturas.Any(x => x.ProveedorId != entrada.ProveedorId)
                || facturas.Select(x => x.TipoProveedorAlRegistrar).Distinct().Count() != 1)
                throw new ReglaPagoException("proveedor_invalido", "Todas las facturas deben pertenecer al proveedor seleccionado.", campo: "facturaIds");
            if (entrada.Medios.Select(x => x.RenglonId).Distinct().Count() != entrada.Medios.Length)
                throw new ReglaPagoException("renglones_repetidos", "Cada renglón debe tener su propia identificación.", campo: "medios");
            var tipo = facturas[0].TipoProveedorAlRegistrar;
            var medios = entrada.Medios.Select((m, i) => CrearMedio(m, tipo, hoy, i)).ToArray();
            if (!ReglasDePagos.MediosPermitidos(tipo, medios.Select(x => x.Tipo)))
                throw new ReglaPagoException("medios_no_admitidos", "El Fletero sólo admite cheques diferidos.", campo: "medios");
            var numeros = new HashSet<(int BancoId, string Numero)>();
            for (var indice = 0; indice < medios.Length; indice++)
                if (medios[indice] is Cheque cheque && (!numeros.Add((cheque.BancoId, cheque.NumeroCheque))
                    || await contexto.Cheques.AnyAsync(c => c.BancoId == cheque.BancoId && c.NumeroCheque == cheque.NumeroCheque, ct)))
                    throw new ReglaPagoException("numero_cheque_duplicado", "Ese número de cheque ya está registrado para el banco.",
                        campo: $"medios[{indice}].numeroCheque");
            if (tipo == TipoProveedor.Fletero)
            {
                await locks.LiquidacionesAsync(facturas.Where(x => x.LiquidacionId.HasValue).Select(x => x.LiquidacionId!.Value), ct);
                var idsLiquidacion = facturas.Select(x => x.LiquidacionId).ToArray();
                var pagos = await contexto.OrdenesDePago.AsNoTracking().Where(x => idsLiquidacion.Contains(x.LiquidacionId)).Select(x => x.Id).ToListAsync(ct);
                await locks.PagosAsync(pagos, ct);
                var generaciones = await contexto.CambiosDeLiquidacion.AsNoTracking()
                    .Where(x => idsLiquidacion.Contains(x.LiquidacionId) && x.Operacion == OperacionDeLiquidacion.Generacion)
                    .Select(x => x.OcurridoEn).ToListAsync(ct);
                if (generaciones.Any(g => !ReglasDeLiquidacion.FechaDePagoValida(hoy, FechaHoyArgentina.Desde(new DateTimeOffset(DateTime.SpecifyKind(g, DateTimeKind.Utc))), hoy)))
                    throw new ReglaPagoException("fecha_pago_invalida", "La fecha del pago no cumple las reglas de la liquidación.", 409);
            }
            var seleccion = await ElegibilidadPagosSql.SeleccionAsync(contexto, entrada.FacturaIds, ct);
            if (seleccion.NoDisponibles.Count != 0)
                throw new ReglaPagoException("facturas_no_disponibles", "Una o más facturas dejaron de estar disponibles. Actualizá la selección.", 409)
                    { FacturasNoDisponibles = seleccion.NoDisponibles.ToArray() };
            var total = seleccion.ImporteTotal; // Única fuente: las FacturaProveedor releídas bajo lock.
            if (!ReglasDePagos.IntentarSumarImportes(medios.Select(x => x.Importe), out var suma) || suma != total)
                throw new ReglaPagoException("total_medios_distinto", "La suma exacta de medios debe coincidir con las facturas.", campo: "medios");
            if (entrada.ImporteTotalConfirmado != total)
                throw new ReglaPagoException("seleccion_modificada", "El total actual de las facturas difiere del revisado. Actualizá la selección.", 409);
            var efectivo = medios.OfType<Efectivo>().Sum(x => x.Importe);
            int? cajaId = null;
            if (efectivo > 0)
            {
                cajaId = await contexto.Cajas.Where(x => x.UsuarioResponsableId == usuarioId && x.Estado == EstadoCaja.Abierta)
                    .Select(x => (int?)x.Id).SingleOrDefaultAsync(ct);
                if (!cajaId.HasValue) throw new ReglaPagoException("caja_no_disponible", "Necesitás tu caja propia abierta.", 409);
                await locks.CajaAsync(cajaId.Value, usuarioId, ct);
                var saldo = await new PrimitivasMovimientosCaja(contexto).SaldoAsync(cajaId.Value, ct);
                if (saldo < efectivo) throw new ReglaPagoException("saldo_insuficiente", "El saldo actual no alcanza para todo el efectivo.", 409)
                    { SaldoDisponible = saldo, ImporteEfectivo = efectivo };
            }
            var proveedor = await contexto.Proveedores.AsNoTracking().SingleAsync(x => x.Id == entrada.ProveedorId, ct);
            var orden = new OrdenPagoProveedor { ProveedorId = entrada.ProveedorId, Fecha = entrada.Fecha!.Value,
                Detalle = ReglasDePagos.NormalizarDetalle(entrada.Detalle), TipoProveedorAlRegistrar = tipo,
                ProveedorRazonSocial = facturas[0].ProveedorRazonSocial, ProveedorCuit = facturas[0].ProveedorCuit,
                ProveedorActivoAlGenerar = proveedor.Activo, ImporteTotal = total, UsuarioId = usuarioId, RegistradaEn = ahoraUtc };
            foreach (var f in facturas) orden.Facturas.Add(new() { FacturaProveedorId = f.Id, ImporteTotalAlPagar = f.ImporteTotal });
            foreach (var m in medios) orden.Medios.Add(m);
            contexto.OrdenesPagoProveedor.Add(orden);
            await contexto.SaveChangesAsync(ct);
            foreach (var factura in facturas) // Ya bloqueadas; modificar también por Id ascendente.
                await contexto.FacturasProveedor.Where(x => x.Id == factura.Id)
                    .ExecuteUpdateAsync(u => u.SetProperty(x => x.Estado, EstadoFacturaProveedor.Pagada), ct);
            if (tipo == TipoProveedor.Fletero)
            {
                var primitiva = new PrimitivasPagosLiquidacion(contexto);
                foreach (var f in facturas.OrderBy(x => x.LiquidacionId))
                    if (await primitiva.RegistrarAsync(f.LiquidacionId!.Value, hoy, f.ImporteTotal, usuarioId, ahoraUtc, orden.Id, ct) is null)
                        throw new ReglaPagoException("liquidacion_no_disponible", "No se pudo aplicar el pago total de la liquidación.", 409);
            }
            if (cajaId.HasValue) await new PrimitivasMovimientosCaja(contexto).InsertarAsync(new()
            {
                CajaId = cajaId.Value, Tipo = TipoMovimientoCaja.Egreso, Importe = efectivo,
                Concepto = "Pago " + NumeroVisiblePagoProveedor.Desde(orden.Numero), UsuarioId = usuarioId,
                Fecha = ahoraUtc, OrdenPagoProveedorId = orden.Id,
            }, ct);
            var detalle = await OrdenAsync(orden.Id, hoy, ct) ?? throw new InvalidOperationException("Falta el recurso creado.");
            var respuesta = new ResultadoEscrituraPago(201, serializador.Serializar(detalle), $"/api/pagos/ordenes/{orden.Id}");
            await resultados.GuardarAsync(TipoOperacionPago.Generar, entrada.ClaveOperacion, comando, usuarioId, orden.Id, respuesta, ahoraUtc, ct);
            commitIntentado = true;
            await tx.CommitAsync(ct);
            return respuesta;
        }
        catch (Exception e) when (commitIntentado && e is not ReglaPagoException)
        {
            contexto.ChangeTracker.Clear();
            return await ResultadosIdempotentesPago.RecuperarAsync(scopes, TipoOperacionPago.Generar, entrada.ClaveOperacion,
                comando, usuarioId, puedeConsultar, ct);
        }
        catch (Exception e) when (BloqueosPagos.Traducir(e) is not null)
        {
            contexto.ChangeTracker.Clear();
            throw BloqueosPagos.Traducir(e)!;
        }
    }

    private static MedioPagoProveedor CrearMedio(MedioEntrada entrada, TipoProveedor tipo, DateOnly hoy, int indice)
    {
        void Campo(bool valido, string campo, string mensaje, string codigo = "medio_invalido")
        {
            if (!valido) throw new ReglaPagoException(codigo, mensaje, campo: $"medios[{indice}].{campo}");
        }
        Campo(ReglasDePagos.ImporteValido(entrada.Importe), "importe", "Ingresá un importe positivo con hasta dos decimales.");
        if (entrada is ChequeEntrada cheque)
        {
            Campo(cheque.BancoId > 0, "bancoId", "Seleccioná un banco Activo.");
            Campo(!string.IsNullOrWhiteSpace(cheque.NumeroCheque), "numeroCheque", "Ingresá el número de cheque.");
            Campo(cheque.NumeroCheque!.Trim().Length <= 200, "numeroCheque",
                "No puede superar 200 caracteres después de quitar los espacios exteriores", "longitud_excedida");
            Campo(cheque.FechaEmision.HasValue && cheque.FechaEmision <= hoy, "fechaEmision", "La emisión es obligatoria y no puede ser futura.");
            Campo(cheque.FechaVencimiento.HasValue && (tipo == TipoProveedor.Fletero
                ? cheque.FechaVencimiento > cheque.FechaEmision : cheque.FechaVencimiento >= cheque.FechaEmision),
                "fechaVencimiento", tipo == TipoProveedor.Fletero ? "El cheque del Fletero debe ser diferido." : "El vencimiento no puede ser anterior a la emisión.");
        }
        if (entrada is TransferenciaEntrada transferencia)
        {
            Campo(transferencia.Fecha.HasValue && transferencia.Fecha <= hoy, "fecha", "La fecha es obligatoria y no puede ser futura.");
            Campo(ReglasDePagos.CbuValido(transferencia.CbuOrigen), "cbuOrigen", "Ingresá exactamente 22 dígitos.");
            Campo(ReglasDePagos.CbuValido(transferencia.CbuDestino), "cbuDestino", "Ingresá exactamente 22 dígitos.");
            Campo(!string.IsNullOrWhiteSpace(transferencia.NumeroComprobante), "numeroComprobante", "Ingresá el comprobante.");
        }
        MedioPagoProveedor medio = entrada switch
        {
            ChequeEntrada c when ReglasDePagos.ChequeValido(tipo, c.BancoId, c.NumeroCheque, c.FechaEmision, c.FechaVencimiento, c.Importe, hoy) =>
                new Cheque { BancoId = c.BancoId, NumeroCheque = c.NumeroCheque!.Trim(), Fecha = c.FechaEmision!.Value,
                    FechaVencimiento = c.FechaVencimiento!.Value, DetalleCheque = c.Detalle },
            TransferenciaEntrada t when tipo == TipoProveedor.ProveedorNormal && ReglasDePagos.TransferenciaValida(t.Fecha, t.Importe, t.CbuOrigen, t.CbuDestino, t.NumeroComprobante, hoy) =>
                new Transferencia { Fecha = t.Fecha!.Value, CbuOrigen = t.CbuOrigen!, CbuDestino = t.CbuDestino!,
                    NumeroComprobante = t.NumeroComprobante!, Observaciones = t.Observaciones },
            EfectivoEntrada e when tipo == TipoProveedor.ProveedorNormal && ReglasDePagos.EfectivoValido(e.Importe) => new Efectivo { Fecha = hoy },
            _ => throw new ReglaPagoException("medio_invalido", "Revisá los campos y las fechas del medio de pago.", campo: "medios"),
        };
        medio.RenglonId = entrada.RenglonId; // Conservar primera escritura; replay ignora UUID incidentales y reproduce su JSON.
        medio.Importe = entrada.Importe;
        return medio;
    }
}
