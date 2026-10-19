import { useEffect, useState, type FormEvent } from 'react'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { Boton } from '../../../compartido/ui/Boton'
import { Campo } from '../../../compartido/ui/Campo'
import { clasesDeControl } from '../../../compartido/ui/clases'
import { Listado, TablaDesplazable } from '../../../compartido/ui/Listado'
import { FilaNavegable } from '../../../compartido/ui/FilaNavegable'
import { EnlaceDeFila } from '../../../compartido/ui/EnlaceDeFila'
import { Paginacion } from '../../../compartido/ui/Paginacion'
import { formatearFecha } from '../../../compartido/fechas'
import { formatearPesos } from '../../../compartido/moneda'
import { AutocompleteProveedorPago } from '../componentes/AutocompleteProveedorPago'
import { consultarOrdenes, mensajePago } from '../servicios/pagos'
import type { EstadoOrden, FiltrosOrdenes, OrdenResumen, Pagina } from '../servicios/tipos'

export function ConsultarOrdenes() {
  const [desde, setDesde] = useState(''); const [hasta, setHasta] = useState('')
  const [proveedorId, setProveedorId] = useState<number>(); const [estado, setEstado] = useState<EstadoOrden | ''>('')
  const [filtros, setFiltros] = useState<FiltrosOrdenes>({ pagina: 1 }); const [clave, setClave] = useState(0)
  const [resultado, setResultado] = useState<Pagina<OrdenResumen> | null>(null)
  const [cargando, setCargando] = useState(true); const [error, setError] = useState(''); const [rango, setRango] = useState('')
  useEffect(() => {
    let vigente = true; setCargando(true); setError('')
    consultarOrdenes(filtros).then(r => { if (vigente) setResultado(r) }).catch(e => { if (vigente) setError(mensajePago(e)) })
      .finally(() => { if (vigente) setCargando(false) })
    return () => { vigente = false }
  }, [filtros])
  function filtrar(e: FormEvent) {
    e.preventDefault()
    if (desde && hasta && desde > hasta) { setRango('Desde no puede ser posterior a Hasta.'); return }
    setRango(''); setFiltros({ desde, hasta, proveedorId, estado, pagina: 1 })
  }
  function limpiar() { setDesde(''); setHasta(''); setProveedorId(undefined); setEstado(''); setRango(''); setClave(k => k + 1); setFiltros({ pagina: 1 }) }
  return <>
    <EncabezadoDePantalla titulo="Consultar órdenes de pago" />
    <form onSubmit={filtrar} className="mb-5 space-y-4">
      <Campo id="orden-desde" etiqueta="Desde" error={rango}><input id="orden-desde" type="date" value={desde} onChange={e => setDesde(e.target.value)} className={clasesDeControl(!!rango)} aria-invalid={!!rango} aria-describedby={rango ? 'orden-desde-error' : undefined} /></Campo>
      <Campo id="orden-hasta" etiqueta="Hasta"><input id="orden-hasta" type="date" value={hasta} onChange={e => setHasta(e.target.value)} className={clasesDeControl()} /></Campo>
      <AutocompleteProveedorPago key={clave} historico onSeleccionar={p => setProveedorId(p?.id)} />
      <Campo id="orden-estado" etiqueta="Estado"><select id="orden-estado" value={estado} onChange={e => setEstado(e.target.value as EstadoOrden | '')} className={clasesDeControl()}><option value="">Todos</option><option value="pendiente">Pendiente</option><option value="anulada">Anulada</option></select></Campo>
      <Boton type="submit" variante="secundario">Filtrar</Boton> <Boton variante="terciario" onClick={limpiar}>Limpiar filtros</Boton>
    </form>
    <Listado>{cargando ? <p role="status" className="p-5">Cargando órdenes…</p> : error ? <p role="alert" className="p-5">{error}</p>
      : resultado?.items.length === 0 ? <p role="status" className="p-5">No hay órdenes que coincidan con los filtros.</p>
      : resultado && <TablaDesplazable><table><caption>Órdenes de pago de proveedores</caption><thead><tr><th scope="col">Número</th><th scope="col">Fecha</th><th scope="col">Proveedor</th><th scope="col">Total</th><th scope="col">Estado</th></tr></thead><tbody>
        {resultado.items.map(o => <FilaNavegable key={o.id} a={`/pagos/ordenes/${o.id}`}><td><EnlaceDeFila a={`/pagos/ordenes/${o.id}`}>{o.numero}</EnlaceDeFila></td><td>{formatearFecha(o.fecha)}</td><td>{o.proveedor.razonSocial}{!o.proveedor.activoActual && ' — Inactivo'}</td><td>{formatearPesos(o.importeTotal)}</td><td>{o.estado === 'pendiente' ? 'Pendiente' : 'Anulada'}</td></FilaNavegable>)}
      </tbody></table></TablaDesplazable>}
      {!cargando && !error && resultado && <Paginacion {...resultado} nombrePlural="órdenes" onCambiarPagina={pagina => setFiltros(f => ({ ...f, pagina }))} />}
    </Listado>
  </>
}
