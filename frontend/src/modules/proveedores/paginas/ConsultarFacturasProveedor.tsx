import { useEffect, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { formatearFecha } from '../../../compartido/fechas'
import { formatearPesos } from '../../../compartido/moneda'
import { formatearCuit } from '../../../compartido/cuit'
import { Boton } from '../../../compartido/ui/Boton'
import { Campo } from '../../../compartido/ui/Campo'
import { clasesDeControl } from '../../../compartido/ui/clases'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { Filtros, FranjaDeBusqueda, FranjaDeFiltros } from '../../../compartido/ui/Filtros'
import { Listado, TablaDesplazable } from '../../../compartido/ui/Listado'
import { Estado } from '../../../compartido/ui/Estado'
import { MensajeListado } from '../componentes/MensajeListado'
import { FilaNavegable, EncabezadoDeChevron } from '../../../compartido/ui/FilaNavegable'
import { Paginacion } from '../../../compartido/ui/Paginacion'
import { AutocompleteProveedor } from '../componentes/AutocompleteProveedor'
import { consultarFacturasProveedor } from '../servicios/facturasProveedor'
import type { FacturaProveedorResumen, FiltrosFacturaProveedor, Pagina, ProveedorResumen } from '../servicios/tipos'

export function ConsultarFacturasProveedor() {
  const [desde, setDesde] = useState(''); const [hasta, setHasta] = useState('')
  const [proveedor, setProveedor] = useState<ProveedorResumen | null>(null)
  const [estado, setEstado] = useState(''); const [claveProveedor, setClaveProveedor] = useState(0)
  const [aplicados, setAplicados] = useState<{ filtros: FiltrosFacturaProveedor; proveedor: ProveedorResumen | null }>({ filtros: {}, proveedor: null })
  const [pagina, setPagina] = useState(1); const [revision, setRevision] = useState(0)
  const [resultado, setResultado] = useState<Pagina<FacturaProveedorResumen> | null>(null)
  const [cargando, setCargando] = useState(true); const [error, setError] = useState(''); const [sinPermiso, setSinPermiso] = useState(false)
  const [errorRango, setErrorRango] = useState('')
  useEffect(() => {
    let vigente = true; setCargando(true); setResultado(null); setError(''); setSinPermiso(false)
    consultarFacturasProveedor(aplicados.filtros, pagina).then(r => { if (vigente) setResultado(r) })
      .catch(e => {
        if (!vigente) return
        const prohibido = e instanceof ErrorHttp && e.estado === 403
        setSinPermiso(prohibido)
        setError(prohibido ? 'No tenés permiso para consultar facturas de proveedores. Pedile acceso a un administrador.' : e instanceof Error ? e.message : 'No pudimos traer las facturas. Volvé a intentar.')
      }).finally(() => { if (vigente) setCargando(false) })
    return () => { vigente = false }
  }, [aplicados, pagina, revision])
  function aplicar(e: FormEvent) {
    e.preventDefault()
    if (desde && hasta && desde > hasta) { setErrorRango('La fecha desde no puede ser posterior a la fecha hasta.'); return }
    setErrorRango('')
    const filtros: FiltrosFacturaProveedor = {}
    if (desde) filtros.desde = desde
    if (hasta) filtros.hasta = hasta
    if (proveedor) filtros.proveedorId = proveedor.id
    if (estado === 'pendiente' || estado === 'pagada') filtros.estado = estado
    setAplicados({ filtros, proveedor }); setPagina(1)
  }
  function limpiar() {
    setDesde(''); setHasta(''); setEstado(''); setProveedor(null); setClaveProveedor(c => c + 1)
    setErrorRango(''); setPagina(1); setAplicados({ filtros: {}, proveedor: null })
  }
  const filtrando = Object.keys(aplicados.filtros).length > 0
  const resumen = [aplicados.filtros.desde && `Desde: ${formatearFecha(aplicados.filtros.desde)}`, aplicados.filtros.hasta && `Hasta: ${formatearFecha(aplicados.filtros.hasta)}`,
    aplicados.proveedor && `Proveedor: ${aplicados.proveedor.razonSocial}`, aplicados.filtros.estado && `Estado: ${aplicados.filtros.estado === 'pagada' ? 'Pagada' : 'Pendiente'}`].filter(Boolean).join(' · ')
  return <>
    <EncabezadoDePantalla titulo="Consultar facturas de proveedores" />
    {error && <p role="alert">{error}</p>}
    {error && !sinPermiso && <Boton variante="secundario" onClick={() => setRevision(r => r + 1)}>Reintentar</Boton>}
    <Listado>
      <form onSubmit={aplicar} noValidate>
        <Filtros declaracion={<span role="status">{filtrando ? `Filtros aplicados: ${resumen}` : 'Sin restricciones de fecha, proveedor ni estado.'}</span>}
          resumen={resultado && <><span role="status">{resultado.total} {resultado.total === 1 ? 'factura' : 'facturas'}</span><span>Ordenado por fecha de factura, de más reciente a más antigua</span></>}>
          <FranjaDeBusqueda><AutocompleteProveedor key={claveProveedor} soloActivos={false} onSeleccionar={setProveedor} /></FranjaDeBusqueda>
          <FranjaDeFiltros>
            <Campo id="desde" etiqueta="Desde" ancho="corto" error={errorRango}><input id="desde" type="date" value={desde} className={clasesDeControl(Boolean(errorRango))} aria-invalid={Boolean(errorRango)} aria-describedby={errorRango ? 'desde-error' : undefined} onChange={e => setDesde(e.target.value)} /></Campo>
            <Campo id="hasta" etiqueta="Hasta" ancho="corto"><input id="hasta" type="date" value={hasta} className={clasesDeControl(false)} onChange={e => setHasta(e.target.value)} /></Campo>
            <Campo id="estado" etiqueta="Estado" ancho="corto"><select id="estado" value={estado} className={clasesDeControl(false)} onChange={e => setEstado(e.target.value)}><option value="">Sin restricción</option><option value="pendiente">Pendiente</option><option value="pagada">Pagada</option></select></Campo>
            <Boton variante="secundario" type="submit">Aplicar filtros</Boton><Boton variante="terciario" onClick={limpiar}>Limpiar filtros</Boton>
          </FranjaDeFiltros>
          {errorRango && <p role="alert">Revisá el rango de fechas.</p>}
        </Filtros>
      </form>
      {cargando && <MensajeListado caso="cargando">Cargando facturas…</MensajeListado>}
      {!cargando && !error && resultado && (resultado.items.length === 0 ? <MensajeListado caso={filtrando ? 'sinCoincidencias' : 'vacio'}>
        {filtrando ? 'Ninguna factura coincide con los filtros aplicados. Probá limpiarlos.' : 'Todavía no hay facturas de proveedores registradas.'}
      </MensajeListado> : <TablaDesplazable><table>
        <caption>Facturas de proveedores</caption>
        <thead><tr><th scope="col">Número de factura</th><th scope="col">Fecha</th><th scope="col">Proveedor</th><th scope="col" className="text-right">Total</th><th scope="col">Estado</th><EncabezadoDeChevron /></tr></thead>
        <tbody>{resultado.items.map(f => <FilaNavegable key={f.id} a={`/proveedores/facturas/${f.id}`}>
          <td><Link className="font-mono font-semibold underline" to={`/proveedores/facturas/${f.id}`}>{f.numero}</Link></td><td>{formatearFecha(f.fecha)}</td>
          <td><span className="block font-semibold">{f.proveedorRazonSocial}</span><span className="font-mono text-faint">{formatearCuit(f.proveedorCuit)}</span></td>
          <td className="text-right font-bold whitespace-nowrap">{formatearPesos(f.importeTotal)}</td><td><Estado valor={f.estado} texto={f.estado === 'pagada' ? 'Pagada' : 'Pendiente'} forma="pastilla" /></td>
        </FilaNavegable>)}</tbody>
      </table></TablaDesplazable>)}
    </Listado>
    {!cargando && !error && resultado && <Paginacion pagina={resultado.pagina} total={resultado.total} tamanioPagina={resultado.tamanioPagina} nombrePlural="facturas" onCambiarPagina={setPagina} />}
  </>
}
