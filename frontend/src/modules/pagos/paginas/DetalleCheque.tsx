import { useEffect, useRef, useState, type FormEvent } from 'react'
import { Link, useParams } from 'react-router-dom'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { FichaEncabezado, FichaSeccion } from '../../../compartido/ui/Ficha'
import { Dialogo, DialogoAcciones } from '../../../compartido/ui/Dialogo'
import { Boton } from '../../../compartido/ui/Boton'
import { Campo } from '../../../compartido/ui/Campo'
import { clasesDeControl } from '../../../compartido/ui/clases'
import { formatearFecha, formatearInstante } from '../../../compartido/fechas'
import { formatearPesos } from '../../../compartido/moneda'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { cambiarEstadoCheque, mensajePago, obtenerCheque } from '../servicios/pagos'
import { destinosCheque, nombresEstadoCheque, nombresSituacionCheque } from '../servicios/estadosCheque'
import { useResultadoPago } from '../componentes/resultadoPago'
import type { DetalleCheque as Cheque, EstadoCheque } from '../servicios/tipos'

export function DetalleCheque({ puedeCambiar = false, puedeConsultarOrden = false }: { puedeCambiar?: boolean; puedeConsultarOrden?: boolean }) {
  const id = Number(useParams().id); const { descartarOrden } = useResultadoPago()
  const [resultado, setResultado] = useState<{ id: number; cheque: Cheque | null; error: string } | null>(null)
  const [revision, setRevision] = useState(0); const [abierto, setAbierto] = useState(false)
  const [nuevo, setNuevo] = useState<EstadoCheque | ''>(''); const [errorEstado, setErrorEstado] = useState('')
  const [ocupado, setOcupado] = useState(false); const [conflicto, setConflicto] = useState(false); const [mensaje, setMensaje] = useState('')
  const accion = useRef<HTMLButtonElement>(null)
  const cheque = resultado?.id === id ? resultado.cheque : null
  useEffect(() => {
    let vigente = true
    obtenerCheque(id).then(cheque => { if (vigente) setResultado({ id, cheque, error: '' }) })
      .catch(e => { if (vigente) setResultado({ id, cheque: null, error: mensajePago(e) }) })
    return () => { vigente = false }
  }, [id, revision])
  function cerrar() { if (ocupado) return; setAbierto(false); requestAnimationFrame(() => accion.current?.focus()) }
  async function guardar(e: FormEvent) {
    e.preventDefault(); if (!cheque || ocupado || !puedeCambiar || conflicto) return
    if (!nuevo) { setErrorEstado('Elegí el nuevo estado.'); return }
    setOcupado(true); setErrorEstado('')
    try {
      const actualizado = await cambiarEstadoCheque(id, cheque.estado, nuevo)
      descartarOrden(actualizado.ordenPagoProveedor.id); setResultado({ id, cheque: actualizado, error: '' }); setAbierto(false); setMensaje('Estado del cheque actualizado.')
    } catch (error) {
      setErrorEstado(mensajePago(error)); if (error instanceof ErrorHttp && error.estado === 409) setConflicto(true)
    } finally { setOcupado(false); requestAnimationFrame(() => accion.current?.focus()) }
  }
  return <>
    <EncabezadoDePantalla titulo="Detalle de cheque" volverA={{ ruta: '/pagos/cheques', etiqueta: 'Volver a cheques' }} />
    {mensaje && <p role="status">{mensaje}</p>}
    {!cheque ? resultado?.id === id && resultado.error ? <p role="alert">{resultado.error}</p> : <p role="status">Cargando cheque…</p> : <>
      <FichaEncabezado identidad={<span className="whitespace-pre-wrap break-all">Cheque {cheque.numeroCheque}</span>} estado={<span>{nombresEstadoCheque[cheque.estado]}</span>}
        resumen={cheque.banco.nombre} acciones={puedeCambiar && destinosCheque[cheque.estado].length > 0 && <Boton ref={accion} variante="secundario" onClick={() => { setNuevo(''); setConflicto(false); setErrorEstado(''); setAbierto(true) }}>Cambiar estado</Boton>} />
      <FichaSeccion id="datos-cheque" titulo="Datos" className="mb-[18px]"><dl><dt>Emisión</dt><dd>{formatearFecha(cheque.fechaEmision)}</dd><dt>Vencimiento</dt><dd>{formatearFecha(cheque.fechaVencimiento)}</dd><dt>Situación</dt><dd>{nombresSituacionCheque[cheque.situacionVencimiento]}</dd><dt>Importe</dt><dd>{formatearPesos(cheque.importe)}</dd><dt>Detalle</dt><dd>{cheque.detalle ?? 'Sin informar'}</dd><dt>Orden</dt><dd>{puedeConsultarOrden ? <Link className="text-brand underline underline-offset-2" to={`/pagos/ordenes/${cheque.ordenPagoProveedor.id}`}>{cheque.ordenPagoProveedor.numero}</Link> : cheque.ordenPagoProveedor.numero}</dd></dl></FichaSeccion>
      <FichaSeccion id="historial-cheque" titulo="Historial de estados">{cheque.historial.length === 0 ? <p className="px-[22px] py-5 text-[13px]">No hay cambios de estado registrados.</p> : <ol className="space-y-3 px-[22px] py-5 text-[13px]">{cheque.historial.map(h => <li key={h.id}>{nombresEstadoCheque[h.estadoAnterior]} → {nombresEstadoCheque[h.estadoNuevo]} · {h.usuario.nombre} · {formatearInstante(h.ocurridoEn)}</li>)}</ol>}</FichaSeccion>
      {abierto && <Dialogo titulo="Cambiar estado del cheque" onCerrar={cerrar}><form onSubmit={guardar} noValidate>
        <p>Estado actual: {nombresEstadoCheque[cheque.estado]}</p>
        <Campo id="cheque-nuevo-estado" etiqueta="Estado nuevo" obligatorio error={nuevo ? null : errorEstado}><select id="cheque-nuevo-estado" required value={nuevo} onChange={e => { setNuevo(e.target.value as EstadoCheque); setErrorEstado('') }} disabled={ocupado || conflicto} className={clasesDeControl(!!errorEstado)} aria-invalid={!!errorEstado} aria-describedby={errorEstado ? 'cheque-error-estado' : undefined}><option value="">Elegir estado</option>{destinosCheque[cheque.estado].map(e => <option key={e} value={e}>{nombresEstadoCheque[e]}</option>)}</select></Campo>
        {errorEstado && <p id="cheque-error-estado" role="alert">{errorEstado}</p>}
        {conflicto && <Boton variante="secundario" onClick={() => { setAbierto(false); setRevision(r => r + 1); requestAnimationFrame(() => accion.current?.focus()) }}>Actualizar información</Boton>}
        <DialogoAcciones><Boton variante="secundario" disabled={ocupado} onClick={cerrar}>Cancelar</Boton><Boton type="submit" variante="primario" disabled={ocupado || conflicto}>{ocupado ? 'Guardando…' : 'Guardar cambio'}</Boton></DialogoAcciones>
      </form></Dialogo>}
    </>}
  </>
}
