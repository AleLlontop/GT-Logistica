import { useEffect, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { formatearFecha } from '../../../compartido/fechas'
import { formatearPesos } from '../../../compartido/moneda'
import { formatearCuit } from '../../../compartido/cuit'
import { Boton } from '../../../compartido/ui/Boton'
import { Campo } from '../../../compartido/ui/Campo'
import { clasesDeControl, clasesDeFiltro } from '../../../compartido/ui/clases'
import { IconoCerrar } from '../../../compartido/ui/iconos'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { Filtros, FranjaDeBusqueda } from '../../../compartido/ui/Filtros'
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
  const [mostrarErrorRango, setMostrarErrorRango] = useState(false)
  const rangoInvertido = Boolean(desde && hasta && desde > hasta)
  const errorRango = mostrarErrorRango && rangoInvertido ? 'La fecha desde no puede ser posterior a la fecha hasta.' : ''
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
    setMostrarErrorRango(rangoInvertido)
    if (rangoInvertido) return
    const filtros: FiltrosFacturaProveedor = {}
    if (desde) filtros.desde = desde
    if (hasta) filtros.hasta = hasta
    if (proveedor) filtros.proveedorId = proveedor.id
    if (estado === 'pendiente' || estado === 'pagada') filtros.estado = estado
    setAplicados({ filtros, proveedor }); setPagina(1)
  }
  function limpiar() {
    setDesde(''); setHasta(''); setEstado(''); setProveedor(null); setClaveProveedor(c => c + 1)
    setMostrarErrorRango(false); setPagina(1); setAplicados({ filtros: {}, proveedor: null })
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
        <Filtros declaracion={<span role="status">{filtrando ? `Filtros aplicados: ${resumen}` : 'Sin filtros aplicados.'}</span>}>
          <FranjaDeBusqueda>
            <AutocompleteProveedor key={claveProveedor} soloActivos={false} comoFiltro onSeleccionar={setProveedor} />
          </FranjaDeBusqueda>
          <div className="grid items-start gap-3.5 sm:grid-cols-3">
            <Campo id="desde" etiqueta="Desde" ancho="completo" error={errorRango} className="min-w-0">
              <input id="desde" type="date" value={desde}
                className={errorRango ? clasesDeControl(true, 'h-10 min-w-0 py-0') : clasesDeFiltro(Boolean(desde), 'h-10 w-full min-w-0')}
                aria-invalid={Boolean(errorRango)} aria-describedby={errorRango ? 'desde-error' : undefined} onChange={e => setDesde(e.target.value)} />
            </Campo>
            <Campo id="hasta" etiqueta="Hasta" ancho="completo" className="min-w-0">
              <input id="hasta" type="date" value={hasta} className={clasesDeFiltro(Boolean(hasta), 'h-10 w-full min-w-0')} onChange={e => setHasta(e.target.value)} />
            </Campo>
            <Campo id="estado" etiqueta="Estado" ancho="completo" className="min-w-0">
              <select id="estado" value={estado} className={clasesDeFiltro(Boolean(estado), 'h-10 w-full min-w-0')} onChange={e => setEstado(e.target.value)}>
                <option value="">Todos</option>
                <option value="pendiente">Pendiente</option>
                <option value="pagada">Pagada</option>
              </select>
            </Campo>
          </div>
          <div className="flex flex-col gap-2.5 sm:flex-row sm:justify-end">
            <Boton variante="secundario" tamanio="chico" type="submit" className="h-10 py-0">Aplicar filtros</Boton>
            <Boton variante="texto" tamanio="chico" className="h-10 px-3 py-0 no-underline hover:bg-surface-hover hover:underline" onClick={limpiar}>
              <IconoCerrar aria-hidden="true" className="size-4" />Limpiar filtros
            </Boton>
          </div>
          {errorRango && <p role="alert">Revisá el rango de fechas.</p>}
        </Filtros>
      </form>
      {cargando && <MensajeListado caso="cargando">Cargando facturas…</MensajeListado>}
      {!cargando && !error && resultado && (resultado.items.length === 0 ? <MensajeListado caso={filtrando ? 'sinCoincidencias' : 'vacio'}>
        {filtrando ? 'Ninguna factura coincide con los filtros aplicados. Probá limpiarlos.' : 'Todavía no hay facturas de proveedores registradas.'}
      </MensajeListado> : <TablaDesplazable><table>
        <caption>Facturas de proveedores</caption>
        <thead><tr><th scope="col">Número de factura</th><th scope="col" aria-sort="descending">Fecha</th><th scope="col">Proveedor</th><th scope="col" className="text-right!">Total</th><th scope="col">Estado</th><EncabezadoDeChevron /></tr></thead>
        <tbody>{resultado.items.map(f => <FilaNavegable key={f.id} a={`/proveedores/facturas/${f.id}`}>
          <td><Link className="font-mono font-semibold whitespace-nowrap underline" to={`/proveedores/facturas/${f.id}`} onClick={e => e.stopPropagation()}>{f.numero}</Link></td><td className="whitespace-nowrap">{formatearFecha(f.fecha)}</td>
          <td><span className="block font-semibold">{f.proveedorRazonSocial}</span><span className="font-mono text-faint">{formatearCuit(f.proveedorCuit)}</span></td>
          <td className="text-right font-bold tabular-nums whitespace-nowrap">{formatearPesos(f.importeTotal)}</td><td><Estado valor={f.estado} texto={f.estado === 'pagada' ? 'Pagada' : 'Pendiente'} forma="pastilla" /></td>
        </FilaNavegable>)}</tbody>
      </table></TablaDesplazable>)}
      {!cargando && !error && resultado && (
        <Paginacion pagina={resultado.pagina} total={resultado.total} tamanioPagina={resultado.tamanioPagina}
          nombrePlural="facturas" nombreSingular="factura" compacta onCambiarPagina={setPagina} />
      )}
    </Listado>
  </>
}
