import { useEffect, useRef, useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { Boton } from '../../../compartido/ui/Boton'
import { Campo } from '../../../compartido/ui/Campo'
import { clasesDeControl } from '../../../compartido/ui/clases'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { BarraDeAcciones, LEYENDA_DE_OBLIGATORIOS } from '../../../compartido/ui/BarraDeAcciones'
import { SeccionNumerada } from '../../../compartido/ui/SeccionNumerada'
import { Listado, TablaDesplazable } from '../../../compartido/ui/Listado'
import { Paginacion } from '../../../compartido/ui/Paginacion'
import { formatearPesos } from '../../../compartido/moneda'
import { formatearFecha } from '../../../compartido/fechas'
import { AutocompleteProveedorPago } from '../componentes/AutocompleteProveedorPago'
import { EditorCheque } from '../componentes/EditorCheque'
import { EditorTransferencia } from '../componentes/EditorTransferencia'
import { EditorEfectivo } from '../componentes/EditorEfectivo'
import { useResultadoPago } from '../componentes/resultadoPago'
import { facturasElegibles, generarOrden, mensajePago, referencias, seleccionarFacturas, SIN_PERMISO_PAGOS } from '../servicios/pagos'
import { sumarImportes } from '../servicios/importesPagos'
import { trimExterior } from '../servicios/normalizacionPagos'
import type { FacturaElegible, GenerarEntrada, MedioEntrada, Pagina, ProveedorOpcion, ReferenciasPago, SeleccionFacturas } from '../servicios/tipos'

type Editor = { tipo: MedioEntrada['tipo']; indice: number | null; errores?: Record<string, string> }
export function GenerarOrden({ puedeGenerar = false, puedeConsultar = false, puedeGestionarBancos = false }: {
  puedeGenerar?: boolean; puedeConsultar?: boolean; puedeGestionarBancos?: boolean
}) {
  const navegar = useNavigate(); const { guardarOrden } = useResultadoPago()
  const [ref, setRef] = useState<ReferenciasPago | null>(null)
  const [cargando, setCargando] = useState(true); const [error, setError] = useState(''); const [sinPermiso, setSinPermiso] = useState(false)
  const [proveedor, setProveedor] = useState<ProveedorOpcion | null>(null)
  const [fecha, setFecha] = useState(''); const [detalle, setDetalle] = useState('')
  const [facturas, setFacturas] = useState<Pagina<FacturaElegible> | null>(null); const [pagina, setPagina] = useState(1)
  const [cargandoFacturas, setCargandoFacturas] = useState(false); const [errorFacturas, setErrorFacturas] = useState('')
  const [ids, setIds] = useState<number[]>([]); const [seleccion, setSeleccion] = useState<SeleccionFacturas | null>(null)
  const [cargandoSeleccion, setCargandoSeleccion] = useState(false); const [errorSeleccion, setErrorSeleccion] = useState('')
  const [medios, setMedios] = useState<MedioEntrada[]>([]); const [tipo, setTipo] = useState<MedioEntrada['tipo']>('cheque')
  const [editor, setEditor] = useState<Editor | null>(null); const [errores, setErrores] = useState<Record<string, string>>({})
  const [ocupado, setOcupado] = useState(false); const [pendiente, setPendiente] = useState<GenerarEntrada | null>(null)
  const [revision, setRevision] = useState(0)
  const fechaInicializada = useRef(false)
  const cacheFacturas = useRef(new Map<number, FacturaElegible>())
  const disparadorEditor = useRef<HTMLElement | null>(null)
  const bloqueado = ocupado || pendiente !== null
  useEffect(() => {
    if (!puedeGenerar) { setCargando(false); return }
    let vigente = true
    referencias().then(r => { if (vigente) { setRef(r); if (!fechaInicializada.current) { setFecha(r.hoyArgentina); fechaInicializada.current = true } } })
      .catch(e => { if (vigente) { setError(mensajePago(e)); if (e instanceof ErrorHttp && e.estado === 403) setSinPermiso(true) } })
      .finally(() => { if (vigente) setCargando(false) })
    return () => { vigente = false }
  }, [puedeGenerar, revision])
  useEffect(() => {
    if (!proveedor) { setFacturas(null); return }
    let vigente = true
    setCargandoFacturas(true); setErrorFacturas('')
    facturasElegibles(proveedor.id, pagina).then(r => {
      if (vigente) { setFacturas(r); r.items.forEach(f => cacheFacturas.current.set(f.id, f)) }
    }).catch(e => { if (vigente) setErrorFacturas(mensajePago(e)) }).finally(() => { if (vigente) setCargandoFacturas(false) })
    return () => { vigente = false }
  }, [proveedor, pagina, revision])
  useEffect(() => {
    if (!ids.length) { setSeleccion(null); setErrorSeleccion(''); setCargandoSeleccion(false); return }
    let vigente = true
    setCargandoSeleccion(true); setErrorSeleccion('')
    seleccionarFacturas(ids).then(r => { if (vigente) setSeleccion(r) })
      .catch(e => { if (vigente) setErrorSeleccion(mensajePago(e)) }).finally(() => { if (vigente) setCargandoSeleccion(false) })
    return () => { vigente = false }
  }, [ids, revision])
  function elegir(p: ProveedorOpcion | null) {
    setProveedor(p); setIds([]); setSeleccion(null); setMedios([]); setPagina(1); setTipo('cheque'); setErrores({})
    cacheFacturas.current.clear()
  }
  function seleccionar(id: number) {
    setIds(actual => actual.includes(id) ? actual.filter(x => x !== id) : [...actual, id].sort((a, b) => a - b))
    setErrores(e => ({ ...e, facturaIds: '' }))
  }
  function cerrarEditor() { setEditor(null); window.requestAnimationFrame(() => disparadorEditor.current?.focus()) }
  function guardarMedio(medio: MedioEntrada) {
    setMedios(actual => editor?.indice === null ? [...actual, medio] : actual.map((m, i) => i === editor?.indice ? medio : m))
    setErrores(e => ({ ...e, medios: '' })); cerrarEditor()
  }
  async function enviar(evento: FormEvent) {
    evento.preventDefault()
    if (ocupado || !ref || !puedeGenerar) return
    let intencion = pendiente
    if (!intencion) {
      const e: Record<string, string> = {}
      if (!proveedor) e.proveedorId = 'Seleccioná un proveedor elegible.'
      if (!fecha || fecha > ref.hoyArgentina) e.fecha = 'La fecha es obligatoria y no puede ser futura.'
      if (!ids.length || !seleccion || seleccion.noDisponibles.length || cargandoSeleccion || errorSeleccion) e.facturaIds = 'Revisá las facturas seleccionadas y su disponibilidad.'
      const suma = sumarImportes(medios.map(m => m.importe))
      if (!medios.length || suma === null || suma !== seleccion?.importeTotal) e.medios = 'Los medios deben cubrir exactamente el total de las facturas.'
      setErrores(e)
      if (Object.keys(e).length || !proveedor || !seleccion) return
      intencion = { claveOperacion: crypto.randomUUID(), proveedorId: proveedor.id, fecha, detalle: trimExterior(detalle) || null,
        facturaIds: [...ids], medios: medios.map(m => ({ ...m })), importeTotalConfirmado: seleccion.importeTotal, confirmado: true }
      setPendiente(intencion)
    }
    setOcupado(true); setError('')
    try { const resultado = await generarOrden(intencion); guardarOrden(resultado); navegar(`/pagos/ordenes/${resultado.id}`) }
    catch (e) {
      setError(mensajePago(e))
      // Una respuesta de entrada/estado es definitiva. Un fallo de red/servidor conserva intención íntegra.
      if (e instanceof ErrorHttp && e.estado >= 400 && e.estado < 500) {
        setPendiente(null)
        if (e.estado === 403) setSinPermiso(true)
        if (e.estado === 409) setRevision(r => r + 1)
        if (e.detalle.campo) {
          const campoMedio = /^medios\[(\d+)\]\.(.+)$/.exec(e.detalle.campo)
          if (campoMedio) {
            const indice = Number(campoMedio[1]); const medio = medios[indice]
            if (medio) setEditor({ tipo: medio.tipo, indice, errores: { [campoMedio[2]]: e.message } })
          } else setErrores(actual => ({ ...actual, [e.detalle.campo!]: e.message }))
        }
      }
    } finally { setOcupado(false) }
  }
  const totalMedios = sumarImportes(medios.map(m => m.importe))
  const inicial = editor?.indice === null || editor === null ? undefined : medios[editor.indice]
  const cancelar = puedeConsultar ? '/pagos/ordenes' : '/'
  return <>
    <EncabezadoDePantalla titulo="Generar orden de pago" />
    {error && !sinPermiso && <p role="alert">{error}</p>}
    {!puedeGenerar || sinPermiso ? <p role="alert">{SIN_PERMISO_PAGOS}</p> : cargando ? <p role="status">Cargando referencias…</p> : ref && <>
      <form noValidate onSubmit={evento => void enviar(evento)} className="rounded-card border border-line bg-surface shadow-card">
        <SeccionNumerada numero={1} titulo="Orden">
          <AutocompleteProveedorPago onSeleccionar={elegir} disabled={bloqueado} error={errores.proveedorId} />
          <Campo id="fecha-opp" etiqueta="Fecha" obligatorio error={errores.fecha}>
            <input id="fecha-opp" type="date" required max={ref.hoyArgentina} value={fecha} disabled={bloqueado}
              className={clasesDeControl(!!errores.fecha)} aria-invalid={!!errores.fecha} aria-describedby={errores.fecha ? 'fecha-opp-error' : undefined}
              onChange={e => { setFecha(e.target.value); setErrores(actual => ({ ...actual, fecha: '' })) }}
              onBlur={() => setErrores(actual => ({ ...actual, fecha: !fecha || fecha > ref.hoyArgentina ? 'La fecha es obligatoria y no puede ser futura.' : '' }))} />
          </Campo>
          <Campo id="detalle-opp" etiqueta="Detalle" ancho="completo"><textarea id="detalle-opp" value={detalle} disabled={bloqueado} className={clasesDeControl()} onChange={e => setDetalle(e.target.value)} /></Campo>
        </SeccionNumerada>
        <SeccionNumerada numero={2} titulo="Facturas">
          {errores.facturaIds && <p role="alert">{errores.facturaIds}</p>}
          {!proveedor ? <p>Elegí un proveedor para consultar sus facturas elegibles.</p> : cargandoFacturas ? <p role="status">Cargando facturas…</p>
            : errorFacturas ? <p role="alert">{errorFacturas}</p> : facturas && <Listado><TablaDesplazable>
              <table><caption>Facturas elegibles del proveedor</caption><thead><tr><th scope="col">Seleccionar</th><th scope="col">Factura</th><th scope="col">Fecha</th><th scope="col">Importe</th></tr></thead>
                <tbody>{facturas.items.map(f => <tr key={f.id}><td><input type="checkbox" disabled={bloqueado} checked={ids.includes(f.id)} aria-label={`Seleccionar factura ${f.numero}`} onChange={() => seleccionar(f.id)} /></td>
                  <td>{f.numero}</td><td>{formatearFecha(f.fecha)}</td><td className="text-right">{formatearPesos(f.importeTotal)}</td></tr>)}</tbody></table>
              {facturas.items.length === 0 && <p role="status" className="p-5">No hay facturas elegibles.</p>}
            </TablaDesplazable><Paginacion {...facturas} nombrePlural="facturas" onCambiarPagina={p => { if (!bloqueado) setPagina(p) }} /></Listado>}
          {ids.length > 0 && <ul aria-label="Facturas seleccionadas">{ids.map(id => <li key={id}>{cacheFacturas.current.get(id)?.numero ?? `Factura ${id}`}
            {seleccion?.noDisponibles.includes(id) && ' — No disponible'} <Boton variante="texto" disabled={bloqueado} onClick={() => seleccionar(id)}>Quitar factura {cacheFacturas.current.get(id)?.numero ?? id}</Boton></li>)}</ul>}
          {cargandoSeleccion && <p role="status">Actualizando total de selección…</p>}{errorSeleccion && <p role="alert">{errorSeleccion}</p>}
          <p>Total de facturas: <strong>{formatearPesos(seleccion?.importeTotal ?? '0.00')}</strong></p>
          {proveedor && <Boton variante="secundario" disabled={bloqueado} onClick={() => setRevision(r => r + 1)}>Actualizar facturas</Boton>}
        </SeccionNumerada>
        <SeccionNumerada numero={3} titulo="Medios de pago">
          {proveedor?.tipo === 'fletero' && <p>Este Fletero se paga exclusivamente con cheques diferidos.</p>}
          <Campo id="tipo-medio" etiqueta="Tipo de medio"><select id="tipo-medio" value={tipo} disabled={!proveedor || bloqueado} className={clasesDeControl()} onChange={e => setTipo(e.target.value as MedioEntrada['tipo'])}>
            <option value="cheque">Cheque</option>{proveedor?.tipo !== 'fletero' && <><option value="transferencia">Transferencia</option><option value="efectivo">Efectivo</option></>}
          </select></Campo>
          <Boton variante="secundario" disabled={!proveedor || bloqueado} onClick={e => { disparadorEditor.current = e.currentTarget; setEditor({ tipo, indice: null }) }}>Agregar medio</Boton>
          {errores.medios && <p role="alert">{errores.medios}</p>}
          {medios.length > 0 && <Listado><TablaDesplazable><table><caption>Medios temporales de la orden</caption><thead><tr><th scope="col">Tipo</th><th scope="col">Importe</th><th scope="col">Acciones</th></tr></thead>
            <tbody>{medios.map((m, i) => <tr key={m.renglonId}><td>{m.tipo === 'cheque' ? 'Cheque' : m.tipo === 'transferencia' ? 'Transferencia' : 'Efectivo'}</td><td className="text-right">{formatearPesos(m.importe)}</td>
              <td><Boton variante="texto" disabled={bloqueado} onClick={e => { disparadorEditor.current = e.currentTarget; setEditor({ tipo: m.tipo, indice: i }) }}>Editar medio {i + 1}</Boton>
                <Boton variante="texto" disabled={bloqueado} onClick={() => setMedios(actual => actual.filter((_, indice) => indice !== i))}>Quitar medio {i + 1}</Boton></td></tr>)}</tbody></table></TablaDesplazable></Listado>}
          <p>Total de medios: <strong>{totalMedios === null ? 'Fuera de capacidad' : formatearPesos(totalMedios)}</strong></p>
          {ref.cajaPropia && proveedor?.tipo !== 'fletero' && <p>Saldo disponible de tu caja: {formatearPesos(ref.cajaPropia.saldoDisponible)}.</p>}
          <p>Revisá las facturas y los medios antes de ejecutar Generar.</p>
        </SeccionNumerada>
        {pendiente && !ocupado && <p role="status" className="p-5">No pudimos conocer el resultado. Reintentá la misma confirmación; la clave y los datos se conservan.</p>}
        <BarraDeAcciones anclaje="viewport" leyenda={LEYENDA_DE_OBLIGATORIOS}>
          <Boton variante="secundario" disabled={bloqueado} onClick={() => navegar(cancelar)}>Cancelar</Boton>
          <Boton variante="primario" type="submit" disabled={ocupado || cargandoSeleccion}>{pendiente ? 'Reintentar generación' : 'Generar'}</Boton>
        </BarraDeAcciones>
      </form>
      {editor?.tipo === 'cheque' && proveedor && <EditorCheque inicial={inicial?.tipo === 'cheque' ? inicial : undefined} hoy={ref.hoyArgentina}
        tipoProveedor={proveedor.tipo} bancos={ref.bancosActivos} puedeGestionarBancos={puedeGestionarBancos} erroresServidor={editor.errores} onGuardar={guardarMedio} onCancelar={cerrarEditor} />}
      {editor?.tipo === 'transferencia' && proveedor?.tipo === 'proveedorNormal' && <EditorTransferencia inicial={inicial?.tipo === 'transferencia' ? inicial : undefined} hoy={ref.hoyArgentina} erroresServidor={editor.errores} onGuardar={guardarMedio} onCancelar={cerrarEditor} />}
      {editor?.tipo === 'efectivo' && proveedor?.tipo === 'proveedorNormal' && <EditorEfectivo inicial={inicial?.tipo === 'efectivo' ? inicial : undefined} hoy={ref.hoyArgentina} erroresServidor={editor.errores} onGuardar={guardarMedio} onCancelar={cerrarEditor} />}
    </>}
  </>
}
