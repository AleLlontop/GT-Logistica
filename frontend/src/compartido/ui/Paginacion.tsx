import { Boton } from './Boton'
import { IconoAnterior, IconoSiguiente } from './iconos'

interface Props {
  pagina: number
  total: number
  tamanioPagina: number
  onCambiarPagina: (pagina: number) => void
  /** Cómo se llaman las filas, para poder decir "20 de 73 choferes" y no "20 de 73 elementos". */
  nombrePlural: string
  /** Nombre de una fila, para el resumen compacto cuando hay un solo resultado. */
  nombreSingular?: string
  /** Presenta el rango y la página por separado. Por defecto conserva el texto histórico. */
  compacta?: boolean
}

/**
 * El control de paginación del sistema. Reemplaza a las cuatro copias que tenían choferes, flota,
 * viajes y facturación, que eran la misma de 41 a 49 líneas con distinto nombre de parámetro.
 *
 * El texto predeterminado conserva la frase histórica, fijada por cuatro tests. La variante compacta
 * separa el rango y la página; Proveedores la usa para no duplicar el total en el resumen de filtros.
 * En ambos casos, `role="status"` avisa que la lista cambió aunque la pantalla no.
 */
export function Paginacion({
  pagina,
  total,
  tamanioPagina,
  onCambiarPagina,
  nombrePlural,
  nombreSingular,
  compacta = false,
}: Props) {
  const paginas = Math.max(1, Math.ceil(total / tamanioPagina))
  const desde = total === 0 ? 0 : (pagina - 1) * tamanioPagina + 1
  const hasta = Math.min(pagina * tamanioPagina, total)

  if (total === 0 || (!compacta && total <= tamanioPagina)) {
    return null
  }

  const controles = (
    <div className="flex items-center gap-2">
      <Boton
        variante="secundario"
        tamanio="chico"
        onClick={() => onCambiarPagina(pagina - 1)}
        disabled={pagina <= 1}
      >
        <IconoAnterior aria-hidden="true" className="size-4" />
        Anterior
      </Boton>

      <Boton
        variante="secundario"
        tamanio="chico"
        onClick={() => onCambiarPagina(pagina + 1)}
        disabled={pagina >= paginas}
      >
        Siguiente
        <IconoSiguiente aria-hidden="true" className="size-4" />
      </Boton>
    </div>
  )

  if (compacta) {
    return (
      <div className="mt-3 flex flex-wrap items-center justify-between gap-3 rounded-card border border-line bg-surface-soft px-5 py-3.5">
        <p role="status" className="m-0 text-[12.5px] text-ink-soft">
          {total === 1 && nombreSingular ? `1 ${nombreSingular}` : `${desde}–${hasta} de ${total} ${nombrePlural}`}
        </p>
        {paginas > 1 && (
          <div className="flex flex-wrap items-center gap-x-3 gap-y-2">
            <span className="text-[12.5px] text-ink-soft">Página {pagina} de {paginas}</span>
            <nav aria-label="Paginación">{controles}</nav>
          </div>
        )}
      </div>
    )
  }

  return (
    <nav
      aria-label="Paginación"
      className="mt-3 flex flex-wrap items-center justify-between gap-3 rounded-card border border-line bg-surface-soft px-5 py-3.5"
    >
      <p role="status" className="m-0 text-[12.5px] text-ink-soft">
        Página {pagina} de {paginas}, mostrando {desde} a {hasta} de {total} {nombrePlural}
      </p>

      {controles}
    </nav>
  )
}
