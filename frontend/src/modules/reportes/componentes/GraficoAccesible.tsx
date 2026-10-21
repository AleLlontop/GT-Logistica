import { Component, lazy, Suspense, useId, useState, type ReactNode } from 'react'
import { formatearPesos } from '../../../compartido/moneda'
import { Boton } from '../../../compartido/ui/Boton'
import { Listado, TablaDesplazable } from '../../../compartido/ui/Listado'
import { centavosAnalisis } from '../servicios/servicioAnalisis'
import type { DatoGrafico, SerieGrafico } from '../servicios/coordenadasGrafico'

const Grafico = lazy(() => import('./GraficosRecharts'))
class LimiteGrafico extends Component<{ children: ReactNode }, { fallo: boolean }> {
  state = { fallo: false }
  static getDerivedStateFromError() { return { fallo: true } }
  render() {
    return this.state.fallo ? <p role="alert" className="text-sm text-danger-text">No pudimos dibujar el gráfico. La tabla de valores exactos sigue disponible.</p> : this.props.children
  }
}

export function GraficoAccesible({ titulo, unidad, datos, series, monetario = false, tipo = 'barras', recorte, onVerCategorias }: {
  titulo: string; unidad: string; datos: DatoGrafico[]; series: SerieGrafico[]; monetario?: boolean; tipo?: 'lineas' | 'barras'; recorte?: string; onVerCategorias?: () => void
}) {
  const id = useId(), [tablaAbierta, setTablaAbierta] = useState(false)
  const hayActividad = datos.some(d => series.some(s => monetario ? centavosAnalisis(d.valores[s.clave] as string) !== 0n : d.valores[s.clave] !== 0))
  return <section aria-labelledby={`${id}-titulo`} className="min-w-0 rounded-card border border-line bg-surface p-4 shadow-card">
    <h3 id={`${id}-titulo`} className="mt-0 mb-2 text-base font-semibold text-ink">{titulo}</h3>
    <p className="mt-0 text-sm text-ink-soft">Unidad: {unidad}. Los valores exactos están en la tabla.</p>
    <ul aria-label={`Series de ${titulo}`} className="flex list-none flex-wrap gap-x-4 gap-y-1 p-0 text-sm text-ink-soft">
      {series.map((serie, indice) => <li key={serie.clave}>{serie.nombre}{series.length > 1 && tipo === 'lineas' ? [' (línea continua)', ' (línea discontinua)', ' (línea punteada)'][indice % 3] : ''}</li>)}
    </ul>
    {recorte && <p className="text-sm text-ink-soft">{recorte}</p>}
    {hayActividad ? <LimiteGrafico key={JSON.stringify(datos)}><Suspense fallback={<p role="status" className="text-sm text-ink-soft">Cargando gráfico… La tabla ya está disponible.</p>}>
      <Grafico titulo={titulo} datos={datos} series={series} monetario={monetario} tipo={tipo} />
    </Suspense></LimiteGrafico> : <p role="status" className="text-sm text-ink-soft">Sin actividad para graficar en este período.</p>}
    <div className="mt-3 flex flex-wrap gap-2">
      <Boton variante="secundario" tamanio="chico" aria-expanded={tablaAbierta} aria-controls={`${id}-tabla`} onClick={() => setTablaAbierta(actual => !actual)}>{tablaAbierta ? 'Ocultar' : 'Ver'} tabla de {titulo}</Boton>
      {onVerCategorias && <Boton variante="secundario" tamanio="chico" onClick={onVerCategorias}>Ver todas las categorías de {titulo}</Boton>}
    </div>
    {tablaAbierta && <div id={`${id}-tabla`} className="mt-3"><Listado><TablaDesplazable>
      <table><caption>{titulo}: valores exactos en {unidad}</caption><thead><tr><th scope="col">Categoría</th>{series.map(s => <th key={s.clave} scope="col" style={{ textAlign: 'right' }}>{s.nombre} ({unidad})</th>)}</tr></thead>
        <tbody>{datos.map((dato, indice) => <tr key={indice}><td>{dato.etiqueta}</td>{series.map(serie => <td key={serie.clave} className="whitespace-nowrap font-semibold tabular-nums" style={{ textAlign: 'right' }}>{monetario ? formatearPesos(dato.valores[serie.clave] as string) : (dato.valores[serie.clave] as number).toLocaleString('es-AR')}</td>)}</tr>)}</tbody>
      </table>
    </TablaDesplazable></Listado></div>}
  </section>
}
