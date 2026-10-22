import type { ReactNode } from 'react'
import { Listado, TablaDesplazable } from '../../../compartido/ui/Listado'
import { Paginacion } from '../../../compartido/ui/Paginacion'
import { formatearPesos } from '../../../compartido/moneda'
import type { PaginaAnalisis } from '../servicios/tiposAnalisis'
import { SinDatosReporte } from './EstadoConsultaAnalisis'
export interface ColumnaAnalisis<T> { titulo: string; valor: (fila: T) => ReactNode; monetaria?: boolean }
export function DineroAnalisis({ valor }: { valor: string | null }) { return <span className="whitespace-nowrap tabular-nums">{valor === null ? 'No corresponde' : formatearPesos(valor)}</span> }
export function TablaAnalisis<T>({ titulo, pagina, columnas, alPaginar, clave }: { titulo: string; pagina: PaginaAnalisis<T>; columnas: ColumnaAnalisis<T>[]; alPaginar: (pagina: number) => void; clave: (fila: T) => string | number }) {
  return <section aria-label={titulo}><h2 className="text-lg font-semibold">{titulo}</h2><p>{pagina.total.toLocaleString('es-AR')} filas en el conjunto filtrado completo.</p>
    {pagina.items.length === 0 ? <SinDatosReporte /> : <Listado><TablaDesplazable><table><caption>{titulo}</caption><thead><tr>{columnas.map(c => <th key={c.titulo} scope="col" style={{ textAlign: c.monetaria ? 'right' : undefined }}>{c.titulo}</th>)}</tr></thead><tbody>{pagina.items.map(fila => <tr key={clave(fila)}>{columnas.map(c => <td key={c.titulo} style={{ textAlign: c.monetaria ? 'right' : undefined }}>{c.valor(fila)}</td>)}</tr>)}</tbody></table></TablaDesplazable></Listado>}
    <Paginacion {...pagina} onCambiarPagina={alPaginar} nombrePlural="filas" />
  </section>
}
