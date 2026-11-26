import { Boton } from './Boton'
import { IconoAnterior, IconoSiguiente } from './iconos'

interface Props {
  pagina: number
  total: number
  tamanioPagina: number
  onCambiarPagina: (pagina: number) => void
  nombrePlural: string
  nombreSingular?: string
  /** Para listados que sólo informan la cantidad de resultados en el pie. */
  mostrarTotalEnUnaPagina?: boolean
}

/**
 * Pie del listado. El rango se anuncia cuando cambia y la página acompaña a los botones.
 */
export function Paginacion({
  pagina,
  total,
  tamanioPagina,
  onCambiarPagina,
  nombrePlural,
  nombreSingular,
  mostrarTotalEnUnaPagina = false,
}: Props) {
  const paginas = Math.max(1, Math.ceil(total / tamanioPagina))
  const desde = (pagina - 1) * tamanioPagina + 1
  const hasta = Math.min(pagina * tamanioPagina, total)

  if (total === 0 || (!mostrarTotalEnUnaPagina && paginas === 1)) {
    return null
  }

  const Contenedor = paginas > 1 ? 'nav' : 'div'
  return (
    <Contenedor
      aria-label={paginas > 1 ? 'Paginación' : undefined}
      className="flex flex-wrap items-center gap-3 border-t border-line bg-surface-soft px-4 py-3.5 sm:px-5"
    >
      <p role="status" className="m-0 text-[12.5px] text-ink-soft">
        {total === 1 && nombreSingular ? `1 ${nombreSingular}` : `${desde}–${hasta} de ${total} ${nombrePlural}`}
      </p>

      {paginas > 1 && (
        <div className="ml-auto flex min-w-0 flex-wrap items-center justify-end gap-x-3 gap-y-2">
          <span className="text-[12.5px] text-ink-soft">Página {pagina} de {paginas}</span>
          <div className="flex min-w-0 flex-wrap items-center justify-end gap-2">
            <Boton variante="secundario" tamanio="chico" onClick={() => onCambiarPagina(pagina - 1)} disabled={pagina <= 1}>
              <IconoAnterior aria-hidden="true" className="size-4" />
              Anterior
            </Boton>
            <Boton variante="secundario" tamanio="chico" onClick={() => onCambiarPagina(pagina + 1)} disabled={pagina >= paginas}>
              Siguiente
              <IconoSiguiente aria-hidden="true" className="size-4" />
            </Boton>
          </div>
        </div>
      )}
    </Contenedor>
  )
}
