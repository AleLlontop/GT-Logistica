import type { ReactNode } from 'react'
import { nombresEstadoCheque } from '../servicios/estadosCheque'
import { Link } from 'react-router-dom'
import { FichaEncabezado, FichaSeccion } from '../../../compartido/ui/Ficha'
import { formatearPesos } from '../../../compartido/moneda'
import { formatearFecha, formatearInstante } from '../../../compartido/fechas'
import type { DetalleOrden } from '../servicios/tipos'

/** Presentación común del DTO íntegro; no reconstruye importes ni identificadores visibles. */
export function DatosOrden({ orden, puedeVerFacturas = false, puedeVerLiquidaciones = false, puedeVerCaja = false, puedeVerCheques = false, acciones }: { acciones?: ReactNode; orden: DetalleOrden; puedeVerFacturas?: boolean; puedeVerLiquidaciones?: boolean; puedeVerCaja?: boolean; puedeVerCheques?: boolean }) {
  return <>
    <FichaEncabezado identidad={orden.numero} estado={<span>{orden.estado === 'pendiente' ? 'Pendiente' : 'Anulada'}</span>}
      acciones={acciones} resumen={<>{orden.proveedor.razonSocial}{!orden.proveedor.activoAlGenerar && ' — Inactivo al generar'}{!orden.proveedor.activoActual && ' — Inactivo actualmente'}</>} />
    <div className="flex min-w-0 flex-col gap-[18px]">
      <FichaSeccion id="datos-orden" titulo="Orden"><dl><dt>Fecha</dt><dd>{formatearFecha(orden.fecha)}</dd>
        <dt>Total</dt><dd>{formatearPesos(orden.importeTotal)}</dd><dt>Detalle</dt><dd>{orden.detalle ?? 'Sin informar'}</dd>
        <dt>Registrada por</dt><dd>{orden.registradaPor.nombre}</dd><dt>Registrada en</dt><dd>{formatearInstante(orden.registradaEn)}</dd></dl></FichaSeccion>
      <FichaSeccion id="facturas-orden" titulo="Facturas"><ul className="space-y-3 px-[22px] py-5 text-[13px] text-ink">{orden.facturas.map(f => <li key={f.id}>
        {puedeVerFacturas ? <Link className="text-brand underline underline-offset-2" to={`/proveedores/facturas/${f.id}`}>{f.numero}</Link> : f.numero} · {formatearPesos(f.importeTotalAlPagar)} · {f.estadoActual === 'pagada' ? 'Pagada' : 'Pendiente'}
        {f.liquidacion && <> · {puedeVerLiquidaciones ? <Link className="text-brand underline underline-offset-2" to={`/liquidaciones/${f.liquidacion.id}`}>{f.liquidacion.numero}</Link> : f.liquidacion.numero} · {f.liquidacion.pago.numero} · {f.liquidacion.pago.vigente ? 'Pago vigente' : 'Pago revertido'}</>}
      </li>)}</ul></FichaSeccion>
      <FichaSeccion id="medios-orden" titulo="Medios de pago"><ul className="divide-y divide-line">{orden.medios.map(m => <li key={m.id} className="text-[13px] text-ink">
        <p className="px-[22px] py-4"><strong>{m.tipo === 'cheque' ? 'Cheque' : m.tipo === 'transferencia' ? 'Transferencia' : 'Efectivo'} · {formatearPesos(m.importe)}</strong> · {formatearFecha(m.fecha)}</p>
        {m.tipo === 'cheque' && <dl><dt>Banco</dt><dd>{m.banco.nombre}</dd><dt>Número</dt><dd className="min-w-0 whitespace-pre-wrap break-all">{puedeVerCheques ? <Link className="text-brand underline underline-offset-2" to={`/pagos/cheques/${m.id}`}>{m.numeroCheque}</Link> : m.numeroCheque}</dd>
          <dt>Vencimiento</dt><dd>{formatearFecha(m.fechaVencimiento)}</dd><dt>Estado</dt><dd>{nombresEstadoCheque[m.estado]}</dd><dt>Detalle</dt><dd>{m.detalle ?? 'Sin informar'}</dd></dl>}
        {m.tipo === 'transferencia' && <dl><dt>CBU origen</dt><dd>{m.cbuOrigen}</dd><dt>CBU destino</dt><dd>{m.cbuDestino}</dd>
          <dt>Comprobante</dt><dd>{m.numeroComprobante}</dd><dt>Observaciones</dt><dd>{m.observaciones ?? 'Sin informar'}</dd></dl>}
      </li>)}</ul></FichaSeccion>
      {orden.caja && <FichaSeccion id="caja-orden" titulo="Caja"><p className="px-[22px] py-4 text-[13px]">{puedeVerCaja && <Link className="text-brand underline underline-offset-2" to={`/caja/${orden.caja.id}`}>Ver caja</Link>} {orden.caja.egreso.concepto} · {formatearPesos(orden.caja.egreso.importe)} · {orden.caja.egreso.usuario.nombre} · {formatearInstante(orden.caja.egreso.fecha)}</p>
        {orden.caja.ingresoCompensatorio && <p className="px-[22px] py-4 text-[13px]">{orden.caja.ingresoCompensatorio.concepto} · {formatearPesos(orden.caja.ingresoCompensatorio.importe)}</p>}
      </FichaSeccion>}
      {orden.anulacion && <FichaSeccion id="anulacion-orden" titulo="Anulación"><dl><dt>Motivo</dt><dd>{orden.anulacion.motivo}</dd><dt>Anulada por</dt><dd>{orden.anulacion.usuario.nombre}</dd><dt>Instante</dt><dd>{formatearInstante(orden.anulacion.ocurridaEn)}</dd></dl></FichaSeccion>}
    </div>
  </>
}
