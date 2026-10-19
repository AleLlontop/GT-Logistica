import { useRef, useState, type FormEvent } from 'react'
import { Boton } from '../../../compartido/ui/Boton'
import { Dialogo, DialogoAcciones } from '../../../compartido/ui/Dialogo'
import { Campo } from '../../../compartido/ui/Campo'
import { clasesDeControl } from '../../../compartido/ui/clases'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { anularOrden, mensajePago } from '../servicios/pagos'
import { trimExterior } from '../servicios/normalizacionPagos'
import type { AnularEntrada, DetalleOrden } from '../servicios/tipos'

export function AnularOrden({ orden, alAnular, alActualizar }: { orden: DetalleOrden; alAnular: (orden: DetalleOrden) => void; alActualizar: () => void }) {
  const [abierto, setAbierto] = useState(false); const [motivo, setMotivo] = useState(''); const [tocado, setTocado] = useState(false)
  const [ocupado, setOcupado] = useState(false); const [error, setError] = useState(''); const [conflicto, setConflicto] = useState(false)
  const [sinPermiso, setSinPermiso] = useState(false); const [incierta, setIncierta] = useState(false)
  const intencion = useRef<AnularEntrada | null>(null); const accion = useRef<HTMLButtonElement>(null)
  const errorMotivo = tocado && !trimExterior(motivo) ? 'Ingresá el motivo de anulación.' : ''
  function cerrar() { if (ocupado || incierta) return; setAbierto(false); requestAnimationFrame(() => accion.current?.focus()) }
  async function confirmar(e: FormEvent) {
    e.preventDefault(); setTocado(true)
    if (!trimExterior(motivo) || ocupado || conflicto || sinPermiso) return
    const entrada = intencion.current ?? { claveOperacion: crypto.randomUUID(), motivo: trimExterior(motivo), confirmado: true }
    intencion.current = entrada; setOcupado(true); setError('')
    try { const actualizada = await anularOrden(orden.id, entrada); setAbierto(false); setIncierta(false); alAnular(actualizada) }
    catch (error) {
      setError(mensajePago(error))
      const definitiva = error instanceof ErrorHttp && error.estado >= 400 && error.estado < 500
      setIncierta(!definitiva)
      if (definitiva) { intencion.current = null; setConflicto(error.estado === 409); setSinPermiso(error.estado === 403) }
    } finally { setOcupado(false); requestAnimationFrame(() => accion.current?.focus()) }
  }
  return <>
    <Boton ref={accion} variante="destructivo" onClick={() => { setMotivo(''); setTocado(false); setError(''); setConflicto(false); setSinPermiso(false); setIncierta(false); intencion.current = null; setAbierto(true) }}>Anular OPP</Boton>
    {abierto && <Dialogo titulo={`Anular ${orden.numero}`} onCerrar={cerrar}><form onSubmit={confirmar} noValidate>
      <p>La orden quedará Anulada. Se conservarán los medios y la historia, se restituirán las facturas y se registrarán las compensaciones admitidas.</p>
      <Campo id="motivo-anulacion" etiqueta="Motivo" obligatorio error={errorMotivo}><textarea id="motivo-anulacion" required value={motivo} onChange={e => setMotivo(e.target.value)} onBlur={() => setTocado(true)} disabled={ocupado || incierta || conflicto || sinPermiso} className={clasesDeControl(!!errorMotivo)} aria-invalid={!!errorMotivo} aria-describedby={errorMotivo ? 'motivo-anulacion-error' : undefined} /></Campo>
      {error && <p role="alert">{error}</p>}
      {incierta && <p role="status">Reintentá esta misma anulación para recuperar el resultado sin repetir las compensaciones.</p>}
      {conflicto && <Boton variante="secundario" onClick={() => { setAbierto(false); alActualizar() }}>Actualizar información</Boton>}
      <DialogoAcciones><Boton variante="secundario" disabled={ocupado || incierta} onClick={cerrar}>Cancelar</Boton>
        <Boton type="submit" variante="destructivo" disabled={ocupado || conflicto || sinPermiso}>{ocupado ? 'Anulando…' : incierta ? 'Reintentar anulación' : 'Confirmar anulación'}</Boton></DialogoAcciones>
    </form></Dialogo>}
  </>
}
