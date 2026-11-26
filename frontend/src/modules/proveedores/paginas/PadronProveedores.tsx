import { useEffect, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { Boton } from '../../../compartido/ui/Boton'
import { clasesDeBoton, clasesDeFiltro, clasesDeCirculoDeIcono } from '../../../compartido/ui/clases'
import { IconoNuevo } from '../../../compartido/ui/iconos'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { Campo } from '../../../compartido/ui/Campo'
import { Listado } from '../../../compartido/ui/Listado'
import { Filtros, FranjaDeBusqueda } from '../../../compartido/ui/Filtros'
import { TablaDesplazable } from '../../../compartido/ui/Listado'
import { FilaNavegable } from '../../../compartido/ui/FilaNavegable'
import { EnlaceDeFila } from '../../../compartido/ui/EnlaceDeFila'
import { MenuDeFila } from '../../../compartido/ui/MenuDeFila'
import { Estado } from '../../../compartido/ui/Estado'
import { MensajeListado } from '../componentes/MensajeListado'
import { Paginacion } from '../../../compartido/ui/Paginacion'
import { DialogoConfirmacion } from '../../../compartido/ui/DialogoConfirmacion'
import { formatearCuit } from '../../../compartido/cuit'
import { listarProveedores, obtenerProveedor, cambiarEstadoProveedor, mensajeProveedor, type FiltrosProveedor } from '../servicios/proveedores'
import type { Pagina, ProveedorResumen, ProveedorDetalle } from '../servicios/tipos'

const INICIAL: FiltrosProveedor = { q: '', razonSocial: '', nombreComercial: '', cuit: '', tipo: '', estado: '', pagina: 1 }
export function PadronProveedores({ puedeGestionar = true }: { puedeGestionar?: boolean }) {
  const [campos, setCampos] = useState(INICIAL)
  const [filtros, setFiltros] = useState(INICIAL)
  const [resultado, setResultado] = useState<Pagina<ProveedorResumen> | null>(null)
  const [error, setError] = useState('')
  const [cargando, setCargando] = useState(true)
  const [revision, setRevision] = useState(0)
  const [baja, setBaja] = useState<ProveedorDetalle | null>(null)
  const [ocupado, setOcupado] = useState(false)
  const [mensaje, setMensaje] = useState('')
  useEffect(() => {
    let vigente = true; setCargando(true); setError('')
    listarProveedores(filtros).then(r => { if (vigente) setResultado(r) })
      .catch(e => { if (vigente) setError(mensajeProveedor(e)) })
      .finally(() => { if (vigente) setCargando(false) })
    return () => { vigente = false }
  }, [filtros, revision])
  function buscar(e: FormEvent) { e.preventDefault(); setFiltros({ ...campos, pagina: 1 }) }
  async function estado(id: number, activo: boolean) {
    if (ocupado) return
    setOcupado(true); setError('')
    try {
      const actual = await obtenerProveedor(id)
      if (!activo) setBaja(actual)
      else { await cambiarEstadoProveedor(id, actual.version, true); setMensaje('Proveedor reactivado.'); setRevision(v => v + 1) }
    } catch (e) { setError(mensajeProveedor(e)) } finally { setOcupado(false) }
  }
  async function confirmarBaja() {
    if (!baja || ocupado) return
    setOcupado(true); setBaja(null)
    try { await cambiarEstadoProveedor(baja.id, baja.version, false); setMensaje('Proveedor dado de baja.'); setRevision(v => v + 1) }
    catch (e) { setError(mensajeProveedor(e)) } finally { setOcupado(false) }
  }
  const filtrado = Object.entries(filtros).some(([k, v]) => k !== 'pagina' && Boolean(v))
  return <>
    <EncabezadoDePantalla titulo="Proveedores" accionPrincipal={puedeGestionar ? <Link className={clasesDeBoton('primario')} to="/proveedores/nuevo">Nuevo proveedor<span className={clasesDeCirculoDeIcono}><IconoNuevo aria-hidden="true" className="size-4" /></span></Link> : undefined} />
    {mensaje && <p role="status">{mensaje}</p>}{error && <p role="alert">{error}</p>}
    <Listado><form onSubmit={buscar}><Filtros declaracion={`Estado: ${filtros.estado || 'Todos'}. ${filtrado ? 'Filtros aplicados.' : 'Sin filtros aplicados.'}`} resumen={`${resultado?.total ?? 0} proveedores. Orden: razón social e identificador.`}>
      <FranjaDeBusqueda><Campo id="buscar" etiqueta="Buscar proveedores" ancho="completo"><input id="buscar" className={clasesDeFiltro(Boolean(campos.q), 'h-11 w-full min-w-0')} value={campos.q} onChange={e => setCampos({ ...campos, q: e.target.value })} /></Campo></FranjaDeBusqueda>
      <div className="grid grid-cols-1 items-end gap-3.5 sm:grid-cols-6 xl:grid-cols-[minmax(0,2fr)_minmax(0,2fr)_minmax(0,1.25fr)_minmax(0,1.5fr)_minmax(0,1fr)]">
        {(['razonSocial', 'nombreComercial', 'cuit'] as const).map((nombre, i) => <Campo key={nombre} id={`f-${nombre}`} etiqueta={['Razón social', 'Nombre comercial', 'CUIT'][i]} ancho="completo" className={`min-w-0 ${nombre === 'cuit' ? 'sm:col-span-2' : 'sm:col-span-3'} xl:col-span-1`}><input id={`f-${nombre}`} className={clasesDeFiltro(Boolean(campos[nombre]), 'h-10 w-full min-w-0')} value={campos[nombre]} onChange={e => setCampos({ ...campos, [nombre]: e.target.value })} /></Campo>)}
        <Campo id="tipo" etiqueta="Tipo" ancho="completo" className="min-w-0 sm:col-span-2 xl:col-span-1"><select id="tipo" className={clasesDeFiltro(Boolean(campos.tipo), 'h-10 w-full min-w-0')} value={campos.tipo} onChange={e => setCampos({ ...campos, tipo: e.target.value })}><option value="">Todos</option><option value="proveedorNormal">Proveedor normal</option><option value="fletero">Fletero</option></select></Campo>
        <Campo id="estado" etiqueta="Estado" ancho="completo" className="min-w-0 sm:col-span-2 xl:col-span-1"><select id="estado" className={clasesDeFiltro(Boolean(campos.estado), 'h-10 w-full min-w-0')} value={campos.estado} onChange={e => setCampos({ ...campos, estado: e.target.value })}><option value="">Todos</option><option value="activo">Activo</option><option value="inactivo">Inactivo</option></select></Campo>
      </div>
      <div className="flex flex-col gap-2.5 sm:flex-row sm:justify-end">
        <Boton variante="secundario" tamanio="chico" type="submit" className="h-10 py-0">Buscar</Boton>
        <Boton variante="terciario" tamanio="chico" className="h-10 py-0" onClick={() => { setCampos(INICIAL); setFiltros({ ...INICIAL }) }}>Limpiar filtros</Boton>
      </div>
    </Filtros></form>
    {cargando ? <p role="status" className="p-5">Cargando proveedores…</p> : !error && resultado && (resultado.items.length === 0
      ? <MensajeListado caso={filtrado ? 'sinCoincidencias' : 'vacio'}>{filtrado ? 'No hay proveedores que coincidan con los filtros.' : 'Todavía no hay proveedores registrados.'}</MensajeListado>
      : <TablaDesplazable><table><caption>Proveedores</caption><thead><tr>{['Razón social', 'Nombre comercial', 'CUIT', 'Tipo', 'Estado', 'Acciones'].map(t => <th key={t} scope="col">{t}</th>)}</tr></thead><tbody>{resultado.items.map(p => <FilaNavegable key={p.id} a={`/proveedores/${p.id}`}>
        <td><EnlaceDeFila a={`/proveedores/${p.id}`}>{p.razonSocial}</EnlaceDeFila></td><td>{p.nombreComercial ?? '—'}</td><td className="font-mono">{formatearCuit(p.cuit)}</td><td>{p.tipo === 'fletero' ? 'Fletero' : 'Proveedor normal'}</td><td><Estado forma="punto" valor={p.estado} texto={p.estado === 'activo' ? 'Activo' : 'Inactivo'} /></td>
        <td>{puedeGestionar && <MenuDeFila etiqueta={`Acciones de ${p.razonSocial}`} items={[{ etiqueta: 'Editar', a: `/proveedores/${p.id}/editar` }, { etiqueta: p.estado === 'activo' ? 'Dar de baja' : 'Reactivar', destructivo: p.estado === 'activo', disabled: ocupado, onSeleccionar: () => void estado(p.id, p.estado !== 'activo') }]} />}</td>
      </FilaNavegable>)}</tbody></table></TablaDesplazable>)}
    {!cargando && !error && resultado && <Paginacion {...resultado} nombrePlural="proveedores" onCambiarPagina={pagina => setFiltros({ ...filtros, pagina })} />}
    </Listado>
    {baja && <DialogoConfirmacion titulo="Dar de baja proveedor" mensaje={`¿Querés dar de baja a ${baja.razonSocial}? Sus datos se conservan.`} etiquetaConfirmar="Confirmar baja" onCancelar={() => setBaja(null)} onConfirmar={() => void confirmarBaja()} />}
  </>
}
