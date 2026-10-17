import { useEffect, useRef, useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { hoyEnIso } from '../../../compartido/fechas'
import { formatearPesos } from '../../../compartido/moneda'
import { Boton } from '../../../compartido/ui/Boton'
import { Campo } from '../../../compartido/ui/Campo'
import { InputImporte } from '../../../compartido/ui/InputImporte'
import { SeccionNumerada } from '../../../compartido/ui/SeccionNumerada'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { BarraDeAcciones, LEYENDA_DE_OBLIGATORIOS } from '../../../compartido/ui/BarraDeAcciones'
import { clasesDeBoton, clasesDeControl } from '../../../compartido/ui/clases'
import { IconoEnRegla } from '../../../compartido/ui/iconos'
import { AutocompleteProveedor } from '../componentes/AutocompleteProveedor'
import { useResultadoFactura } from '../componentes/resultadoFactura'
import { cargarFacturaProveedor, consultarLiquidacionesDisponibles } from '../servicios/facturasProveedor'
import { calcularImportes } from '../servicios/importesFactura'
import type { ProveedorResumen, LiquidacionDisponible, ConflictoImportes } from '../servicios/tipos'

export function CargarFacturaProveedor({ puedeCargar = true }: { puedeCargar?: boolean }) {
  const navegar = useNavigate(); const { guardar } = useResultadoFactura()
  const [proveedor, setProveedor] = useState<ProveedorResumen | null>(null)
  const [liquidaciones, setLiquidaciones] = useState<LiquidacionDisponible[]>([])
  const [liquidacionId, setLiquidacionId] = useState('')
  const [cargandoLiquidaciones, setCargandoLiquidaciones] = useState(false)
  const [errorLiquidaciones, setErrorLiquidaciones] = useState('')
  const [conflicto, setConflicto] = useState<ConflictoImportes | null>(null)
  const secuenciaLiquidaciones = useRef(0)
  useEffect(() => () => { secuenciaLiquidaciones.current++ }, [])
  const [datos, setDatos] = useState({ numero: '', fecha: hoyEnIso(), detalle: '', neto: '', cae: '', fechaVencimientoCae: '' })
  const [archivo, setArchivo] = useState<File | undefined>()
  const [errores, setErrores] = useState<Record<string, string>>({}); const [error, setError] = useState('')
  const [guardando, setGuardando] = useState(false)
  const entradaNeto = useRef('')
  const campoPdf = useRef<HTMLInputElement>(null)
  const importes = calcularImportes(datos.neto)
  const liquidacionValida = liquidaciones.some(l => String(l.id) === liquidacionId) && !cargandoLiquidaciones && !errorLiquidaciones
  async function cargarLiquidaciones(p: ProveedorResumen, conservarSeleccion = false) {
    const secuencia = ++secuenciaLiquidaciones.current
    setCargandoLiquidaciones(true); setErrorLiquidaciones('')
    if (!conservarSeleccion) { setLiquidacionId(''); setLiquidaciones([]) }
    try {
      const disponibles = await consultarLiquidacionesDisponibles(p.id)
      if (secuencia !== secuenciaLiquidaciones.current) return
      setLiquidaciones(disponibles)
      setLiquidacionId(actual => disponibles.some(l => String(l.id) === actual) ? actual : '')
    } catch (e) {
      if (secuencia !== secuenciaLiquidaciones.current) return
      setLiquidacionId(''); setLiquidaciones([])
      setErrorLiquidaciones(e instanceof ErrorHttp && e.estado === 403 ? 'No tenés permiso para cargar facturas. Pedile acceso a un administrador.' : e instanceof Error ? e.message : 'No pudimos traer las liquidaciones. Volvé a intentar.')
    } finally { if (secuencia === secuenciaLiquidaciones.current) setCargandoLiquidaciones(false) }
  }
  function seleccionarProveedor(p: ProveedorResumen | null) {
    secuenciaLiquidaciones.current++; setProveedor(p); setLiquidacionId(''); setLiquidaciones([])
    setCargandoLiquidaciones(false); setErrorLiquidaciones(''); setConflicto(null); setErrores({}); setError('')
    if (p?.tipo === 'fletero') void cargarLiquidaciones(p)
  }
  async function enviar(e: FormEvent) {
    e.preventDefault(); if (guardando) return
    const encontrados: Record<string, string> = {}
    if (!proveedor) encontrados.proveedorId = 'Elegí un proveedor activo de las coincidencias.'
    if (proveedor?.tipo === 'fletero' && !liquidacionValida) encontrados.liquidacionId = 'Elegí una liquidación Pendiente disponible del Fletero.'
    if (!datos.numero.trim()) encontrados.numero = 'Escribí el número de factura.'
    if (!datos.detalle.trim()) encontrados.detalle = 'Escribí el detalle.'
    if (!importes) encontrados.neto = 'Ingresá un neto mayor a cero, con hasta dos decimales y dentro de la capacidad de almacenamiento.'
    if (!datos.cae.trim()) encontrados.cae = 'Escribí el CAE.'
    if (!datos.fecha || datos.fecha > hoyEnIso()) encontrados.fecha = 'La fecha no puede ser posterior a hoy en Argentina.'
    if (!datos.fechaVencimientoCae || datos.fechaVencimientoCae < datos.fecha) encontrados.fechaVencimientoCae = 'El vencimiento debe ser igual o posterior a la fecha de factura.'
    if (archivo && (archivo.size === 0 || archivo.size > 10 * 1024 * 1024)) encontrados.archivo = 'El PDF debe tener contenido y pesar como máximo 10 MB.'
    setErrores(encontrados); setError(''); setConflicto(null)
    if (Object.keys(encontrados).length || !proveedor || !importes) return
    setGuardando(true)
    try {
      const resultado = await cargarFacturaProveedor({ proveedorId: proveedor.id, numero: datos.numero, fecha: datos.fecha, detalle: datos.detalle, neto: importes.neto, cae: datos.cae, fechaVencimientoCae: datos.fechaVencimientoCae, archivo, liquidacionId: proveedor.tipo === 'fletero' ? Number(liquidacionId) : undefined })
      guardar(resultado)
      navegar(`/proveedores/facturas/${resultado.id}`, { replace: true })
    } catch (e) {
      if (e instanceof ErrorHttp && e.estado === 409 && e.detalle.campo === 'liquidacionId') {
        const detalle = e.detalle as ConflictoImportes
        if (typeof detalle.importeTotalFactura === 'string' && typeof detalle.importeTotalLiquidacion === 'string') setConflicto(detalle)
        if (proveedor.tipo === 'fletero') void cargarLiquidaciones(proveedor, true)
      }
      if (e instanceof ErrorHttp && e.detalle.campo && e.estado !== 403) setErrores({ [e.detalle.campo]: e.message })
      else setError(e instanceof ErrorHttp && e.estado === 403 ? 'No tenés permiso para cargar facturas. Pedile acceso a un administrador.' : e instanceof Error ? e.message : 'No pudimos guardar la factura. Volvé a intentar.')
    } finally { setGuardando(false) }
  }
  function campo(nombre: 'numero' | 'fecha' | 'cae' | 'fechaVencimientoCae', etiqueta: string, fecha = false) {
    return <Campo id={nombre} etiqueta={etiqueta} obligatorio error={errores[nombre]} ancho={fecha ? 'corto' : 'medio'}><input id={nombre} type={fecha ? 'date' : 'text'} required value={datos[nombre]} disabled={guardando}
      className={clasesDeControl(Boolean(errores[nombre]))} aria-invalid={Boolean(errores[nombre])} aria-describedby={errores[nombre] ? `${nombre}-error` : undefined} onChange={e => setDatos(d => ({ ...d, [nombre]: e.target.value }))} /></Campo>
  }
  return <>
    <EncabezadoDePantalla titulo="Cargar factura de proveedor" volverA={{ ruta: '/', etiqueta: 'Volver al inicio' }} />
    {!puedeCargar ? <p role="alert">No tenés permiso para cargar facturas. Pedile acceso a un administrador.</p> : <>
      {error && <p role="alert">{error}</p>}
      {conflicto && <dl aria-live="polite"><dt>Total de factura informado por el servidor</dt><dd>{formatearPesos(conflicto.importeTotalFactura)}</dd><dt>Total actual de liquidación informado por el servidor</dt><dd>{formatearPesos(conflicto.importeTotalLiquidacion)}</dd></dl>}
      {Object.values(errores).some(Boolean) && <p role="alert">Revisá los campos marcados.</p>}
      <form onSubmit={enviar} noValidate className="overflow-hidden rounded-card border border-line bg-surface shadow-card">
        <SeccionNumerada numero={1} titulo="Proveedor"><AutocompleteProveedor soloActivos disabled={guardando} error={errores.proveedorId} onSeleccionar={seleccionarProveedor} />
          {proveedor?.tipo === 'fletero' && <>
            {cargandoLiquidaciones && <p role="status">Cargando liquidaciones…</p>}
            {errorLiquidaciones && <p role="alert">{errorLiquidaciones}</p>}
            {errorLiquidaciones && !errorLiquidaciones.includes('permiso') && <Boton variante="secundario" onClick={() => void cargarLiquidaciones(proveedor)}>Reintentar liquidaciones</Boton>}
            {!cargandoLiquidaciones && !errorLiquidaciones && liquidaciones.length === 0 && <p role="status">No hay liquidaciones Pendientes disponibles para este Fletero. No se puede guardar la factura.</p>}
            <Campo id="liquidacionId" etiqueta="Liquidación" ancho="largo" obligatorio error={errores.liquidacionId}>
              <select id="liquidacionId" required value={liquidacionId} disabled={guardando || cargandoLiquidaciones || liquidaciones.length === 0}
                className={clasesDeControl(Boolean(errores.liquidacionId))} aria-invalid={Boolean(errores.liquidacionId)} aria-describedby={errores.liquidacionId ? 'liquidacionId-error' : undefined}
                onChange={e => { setLiquidacionId(e.target.value); setConflicto(null); setErrores(actual => ({ ...actual, liquidacionId: '' })) }}>
                <option value="">Elegí una liquidación</option>
                {liquidaciones.map(l => <option key={l.id} value={l.id}>{l.numero} · {l.periodo} · {formatearPesos(l.importeTotal)}</option>)}
              </select>
            </Campo>
          </>}
        </SeccionNumerada>
        <SeccionNumerada numero={2} titulo="Datos de factura">{campo('numero', 'Número de factura')}{campo('fecha', 'Fecha de factura', true)}
          <Campo id="detalle" etiqueta="Detalle" ancho="completo" obligatorio error={errores.detalle}><textarea id="detalle" required disabled={guardando} value={datos.detalle} className={clasesDeControl(Boolean(errores.detalle))} aria-invalid={Boolean(errores.detalle)} aria-describedby={errores.detalle ? 'detalle-error' : undefined} onChange={e => setDatos(d => ({ ...d, detalle: e.target.value }))} /></Campo>
          {campo('cae', 'CAE')}{campo('fechaVencimientoCae', 'Vencimiento del CAE', true)}
        </SeccionNumerada>
        <SeccionNumerada numero={3} titulo="Importes y comprobante">
          <Campo id="neto" etiqueta="Neto" ancho="corto" obligatorio error={errores.neto}>
            <InputImporte id="neto" required disabled={guardando} value={datos.neto}
              className={clasesDeControl(Boolean(errores.neto))} aria-invalid={Boolean(errores.neto)} aria-describedby={errores.neto ? 'neto-error' : undefined}
              onChangeCapture={e => {
                // Validar el signo antes de que el control compartido lo quite al formatear.
                entradaNeto.current = e.currentTarget.value
              }}
              onChange={neto => {
                const ingresado = entradaNeto.current
                setDatos(d => ({ ...d, neto: ingresado.includes('-') ? ingresado : neto }))
              }} />
          </Campo>
          <dl aria-live="polite"><dt>IVA (21%)</dt><dd>{importes ? formatearPesos(importes.iva) : '—'}</dd><dt>Total</dt><dd>{importes ? formatearPesos(importes.importeTotal) : '—'}</dd></dl>
          <Campo id="archivo" etiqueta="PDF" ayuda="PDF opcional, hasta 10 MB." error={errores.archivo}><input ref={campoPdf} id="archivo" type="file" accept="application/pdf,.pdf" disabled={guardando} className={clasesDeControl(Boolean(errores.archivo))} aria-describedby={errores.archivo ? 'archivo-error' : 'archivo-ayuda'} onChange={e => setArchivo(e.target.files?.[0])} /></Campo>
          {archivo && <Boton variante="terciario" disabled={guardando} onClick={() => { setArchivo(undefined); if (campoPdf.current) campoPdf.current.value = '' }}>Quitar PDF</Boton>}
        </SeccionNumerada>
        <BarraDeAcciones anclaje="viewport" leyenda={LEYENDA_DE_OBLIGATORIOS}><Link to="/" className={clasesDeBoton('secundario')}>Cancelar</Link><Boton variante="primario" icono={<IconoEnRegla />} type="submit" disabled={guardando || (proveedor?.tipo === 'fletero' && !liquidacionValida)}>{guardando ? 'Guardando…' : 'Guardar factura'}</Boton></BarraDeAcciones>
      </form>
    </>}
  </>
}
