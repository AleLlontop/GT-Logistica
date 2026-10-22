import { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { AnularOrden } from '../componentes/AnularOrden'
import { DatosOrden } from '../componentes/DatosOrden'
import { useResultadoPago } from '../componentes/resultadoPago'
import { mensajePago, obtenerOrden } from '../servicios/pagos'
import type { DetalleOrden as Orden } from '../servicios/tipos'

export interface PermisosDetalleOrden { puedeConsultar?: boolean; puedeAnular?: boolean; puedeVerFacturas?: boolean; puedeVerLiquidaciones?: boolean; puedeVerCaja?: boolean; puedeVerCheques?: boolean }
export function DetalleOrden(permisos: PermisosDetalleOrden) {
  const id = Number(useParams().id); const { orden: propia, guardarOrden, descartarOrden } = useResultadoPago()
  const [revision, setRevision] = useState(0)
  const coincidente = propia?.id === id ? propia : null
  const [resultado, setResultado] = useState<{ id: number; orden: Orden | null; error: string } | null>(null)
  useEffect(() => {
    if (coincidente) return
    let vigente = true
    obtenerOrden(id).then(orden => { if (vigente) setResultado({ id, orden, error: '' }) })
      .catch(e => { if (vigente) setResultado({ id, orden: null, error: mensajePago(e) }) })
    return () => { vigente = false }
  }, [id, coincidente, revision])
  const datos = coincidente ?? (resultado?.id === id ? resultado.orden : null)
  const error = resultado?.id === id ? resultado.error : ''
  return <>
    <EncabezadoDePantalla titulo="Orden de pago" volverA={{ ruta: permisos.puedeConsultar ? '/pagos/ordenes' : '/', etiqueta: permisos.puedeConsultar ? 'Volver a órdenes' : 'Volver al inicio' }} />
    {datos ? <>{coincidente && <p role="status">{datos.estado === 'anulada' ? 'Orden anulada correctamente.' : 'Orden generada correctamente.'}</p>}
      <DatosOrden orden={datos} {...permisos} acciones={permisos.puedeConsultar && permisos.puedeAnular && datos.estado === 'pendiente' && !datos.medios.some(m => m.tipo === 'transferencia' || m.tipo === 'cheque' && m.estado !== 'pendiente') && <AnularOrden key={datos.id} orden={datos} alAnular={guardarOrden} alActualizar={() => { descartarOrden(id); setResultado(null); setRevision(r => r + 1) }} />} /></>
      : error ? <p role="alert">{error}</p> : <p role="status">Cargando orden…</p>}
  </>
}
