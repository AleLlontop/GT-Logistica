import { useEffect, useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { BarraDeAcciones, LEYENDA_DE_OBLIGATORIOS } from '../../../compartido/ui/BarraDeAcciones'
import { Boton } from '../../../compartido/ui/Boton'
import { Campo } from '../../../compartido/ui/Campo'
import { clasesDeBoton, clasesDeControl } from '../../../compartido/ui/clases'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { SeccionNumerada } from '../../../compartido/ui/SeccionNumerada'
import { formatearCuit } from '../../../compartido/cuit'
import { IconoEnRegla } from '../../../compartido/ui/iconos'
import { guardarProveedor, obtenerProveedor, listarExternos, mensajeProveedor, SIN_PERMISO } from '../servicios/proveedores'
import type { Pagina, ProveedorDetalle, ProveedorPeticion, TransportistaExterno } from '../servicios/tipos'

const VACIO: ProveedorPeticion = { tipo: 'proveedorNormal', razonSocial: '', cuit: '', nombreComercial: '', telefono: '', email: '', direccion: '' }
export function FormularioProveedor({ puedeGestionar = true }: { puedeGestionar?: boolean }) {
  const { id } = useParams(); const idProveedor = id === undefined ? undefined : Number(id)
  const navegar = useNavigate()
  const [datos, setDatos] = useState(VACIO)
  const [origen, setOrigen] = useState<ProveedorDetalle | null>(null)
  const [cargando, setCargando] = useState(id !== undefined)
  const [error, setError] = useState('')
  const [errores, setErrores] = useState<Record<string, string>>({})
  const [guardando, setGuardando] = useState(false)
  const [q, setQ] = useState(''); const [pagina, setPagina] = useState(1)
  const [externos, setExternos] = useState<Pagina<TransportistaExterno> | null>(null)
  const [seleccionLista, setSeleccionLista] = useState(false)
  const [errorExternos, setErrorExternos] = useState('')
  const [revision, setRevision] = useState(0)
  useEffect(() => {
    if (idProveedor === undefined) return
    let vigente = true; setCargando(true)
    obtenerProveedor(idProveedor).then(p => {
      if (!vigente) return
      setDatos({ tipo: p.tipo, razonSocial: p.tipo === 'fletero' ? '' : p.razonSocial,
        cuit: p.tipo === 'fletero' ? '' : p.cuit, telefono: p.tipo === 'fletero' ? '' : p.telefono ?? '',
        email: p.tipo === 'fletero' ? '' : p.email ?? '', nombreComercial: p.nombreComercial ?? '',
        direccion: p.direccion ?? '', transportistaId: p.transportistaId ?? undefined, version: p.version })
      setOrigen(p); setQ(p.tipo === 'fletero' ? p.cuit : ''); setPagina(1)
    }).catch(e => { if (vigente) setError(mensajeProveedor(e)) })
      .finally(() => { if (vigente) setCargando(false) })
    return () => { vigente = false }
  }, [idProveedor])
  useEffect(() => {
    if (cargando || datos.tipo !== 'fletero') return
    let vigente = true; setSeleccionLista(false); setErrorExternos('')
    listarExternos(q, pagina).then(r => {
      if (!vigente) return
      setExternos(r)
      setDatos(d => r.items.some(t => t.id === d.transportistaId) ? d : { ...d, transportistaId: undefined })
      setSeleccionLista(true)
    }).catch(e => { if (vigente) { setErrorExternos(mensajeProveedor(e)); setExternos(null) } })
    return () => { vigente = false }
  }, [cargando, datos.tipo, q, pagina, revision])
  function actualizar(campo: keyof ProveedorPeticion, valor: string) { setDatos(d => ({ ...d, [campo]: valor })) }
  function buscar(valor: string) { setSeleccionLista(false); setQ(valor); setPagina(1) }
  async function enviar(e: FormEvent) {
    e.preventDefault()
    if (guardando || cargando || (datos.tipo === 'fletero' && !seleccionLista)) return
    setError(''); setErrores({})
    const invalido: Record<string, string> = {}
    if (datos.tipo === 'proveedorNormal') {
      if (!datos.razonSocial?.trim()) invalido.razonSocial = 'Escribí la razón social.'
      if (!datos.cuit?.trim()) invalido.cuit = 'Escribí el CUIT.'
    } else if (!datos.transportistaId) invalido.transportistaId = 'Elegí un transportista externo.'
    if (Object.keys(invalido).length) { setErrores(invalido); return }
    setGuardando(true)
    const peticion: ProveedorPeticion = datos.tipo === 'fletero'
      ? { tipo: 'fletero', transportistaId: datos.transportistaId, nombreComercial: datos.nombreComercial, direccion: datos.direccion, version: datos.version }
      : { ...datos, transportistaId: undefined }
    try {
      const creado = await guardarProveedor(idProveedor, peticion)
      navegar(`/proveedores/${creado.id}`, { replace: true, state: { mensaje: id === undefined ? 'Proveedor registrado.' : 'Cambios guardados.' } })
    } catch (e) {
      if (e instanceof ErrorHttp && e.detalle.campo) setErrores({ [e.detalle.campo]: mensajeProveedor(e) })
      else setError(mensajeProveedor(e))
    } finally { setGuardando(false) }
  }
  const elegido = externos?.items.find(t => t.id === datos.transportistaId)
  const bloqueado = guardando || cargando
  function campo(nombre: 'razonSocial' | 'cuit' | 'telefono' | 'email' | 'nombreComercial' | 'direccion', etiqueta: string, obligatorio = false) {
    return <Campo id={nombre} etiqueta={etiqueta} obligatorio={obligatorio} error={errores[nombre]} ancho={nombre === 'cuit' ? 'corto' : ['razonSocial', 'direccion'].includes(nombre) ? 'largo' : 'medio'}>
      <input id={nombre} className={clasesDeControl(Boolean(errores[nombre]))} required={obligatorio} type={nombre === 'email' ? 'email' : 'text'} value={datos[nombre] ?? ''} onChange={e => actualizar(nombre, e.target.value)} disabled={bloqueado} aria-invalid={Boolean(errores[nombre])} aria-describedby={errores[nombre] ? `${nombre}-error` : undefined} />
    </Campo>
  }
  return <>
    <EncabezadoDePantalla titulo={id === undefined ? 'Nuevo proveedor' : 'Editar proveedor'} volverA={{ ruta: '/proveedores', etiqueta: 'Volver a proveedores' }} />
    {!puedeGestionar ? <p role="alert">{SIN_PERMISO}</p> : <>
    {error && <p role="alert">{error}</p>}
    {Object.values(errores).some(Boolean) && <p role="alert">Revisá los campos marcados.</p>}
    {cargando ? <p role="status">Cargando proveedor…</p> : (id === undefined || origen) && <form onSubmit={enviar} noValidate className="overflow-hidden rounded-card border border-line bg-surface shadow-card">
      <SeccionNumerada numero={1} titulo="Identificación">
        <Campo id="tipo" etiqueta="Tipo" obligatorio><select id="tipo" required disabled={bloqueado} className={clasesDeControl()} value={datos.tipo} onChange={e => { setSeleccionLista(false); setDatos(d => ({ ...d, tipo: e.target.value as ProveedorPeticion['tipo'], transportistaId: undefined })); setErrores({}) }}><option value="proveedorNormal">Proveedor normal</option><option value="fletero">Fletero</option></select></Campo>
        {datos.tipo === 'proveedorNormal' ? <>{campo('razonSocial', 'Razón social', true)}{campo('cuit', 'CUIT', true)}</> : <>
          <Campo id="buscar-transportista" etiqueta="Buscar transportista" ancho="largo"><input id="buscar-transportista" className={clasesDeControl()} disabled={bloqueado} value={q} onChange={e => buscar(e.target.value)} /></Campo>
          <Campo id="transportista" etiqueta="Transportista" obligatorio error={errores.transportistaId} ancho="largo"><select id="transportista" required className={clasesDeControl(Boolean(errores.transportistaId))} disabled={bloqueado || !seleccionLista} value={datos.transportistaId ?? ''} aria-invalid={Boolean(errores.transportistaId)} aria-describedby={errores.transportistaId ? 'transportista-error' : undefined} onChange={e => setDatos(d => ({ ...d, transportistaId: Number(e.target.value) || undefined }))}><option value="">Elegí un transportista</option>{(seleccionLista ? externos?.items : [])?.map(t => <option key={t.id} value={t.id}>{t.razonSocial} — {formatearCuit(t.cuit)}</option>)}</select></Campo>
          {errorExternos ? <><p role="alert">{errorExternos}</p>{!errorExternos.includes('Pedile acceso') && <Boton variante="secundario" onClick={() => setRevision(v => v + 1)}>Reintentar</Boton>}</> : !seleccionLista ? <p role="status">Cargando transportistas…</p> : externos?.items.length === 0 ? <p role="status">No hay transportistas externos que coincidan.</p> : <p role="status">Página {pagina}. {externos?.total} transportistas externos.</p>}
          <nav aria-label="Páginas de transportistas" className="flex gap-3"><Boton variante="secundario" disabled={bloqueado || !seleccionLista || pagina <= 1} onClick={() => { setSeleccionLista(false); setPagina(p => p - 1) }}>Anterior</Boton><Boton variante="secundario" disabled={bloqueado || !seleccionLista || pagina * 20 >= (externos?.total ?? 0)} onClick={() => { setSeleccionLista(false); setPagina(p => p + 1) }}>Siguiente</Boton></nav>
          {elegido && <p>{elegido.razonSocial} · {formatearCuit(elegido.cuit)}</p>}
          <p>La razón social, el CUIT, el teléfono y el email se mantienen en Transportistas.</p>
          {origen?.tipo === 'fletero' && origen.transportistaId === datos.transportistaId && <dl><dt>Teléfono de origen</dt><dd>{origen.telefono ?? 'Sin informar'}</dd><dt>Email de origen</dt><dd>{origen.email ?? 'Sin informar'}</dd></dl>}
        </>}
        {campo('nombreComercial', 'Nombre comercial')}
      </SeccionNumerada>
      <SeccionNumerada numero={2} titulo="Contacto">{datos.tipo === 'proveedorNormal' && <>{campo('telefono', 'Teléfono')}{campo('email', 'Email')}</>}{campo('direccion', 'Dirección')}</SeccionNumerada>
      <BarraDeAcciones anclaje="viewport" leyenda={LEYENDA_DE_OBLIGATORIOS}><Link className={clasesDeBoton('secundario')} to={id === undefined ? '/proveedores' : `/proveedores/${id}`}>Cancelar</Link><Boton variante="primario" icono={<IconoEnRegla />} type="submit" disabled={bloqueado || (datos.tipo === 'fletero' && !seleccionLista)}>{guardando ? 'Guardando…' : id === undefined ? 'Guardar proveedor' : 'Guardar cambios'}</Boton></BarraDeAcciones>
    </form>}
    </>}
  </>
}
