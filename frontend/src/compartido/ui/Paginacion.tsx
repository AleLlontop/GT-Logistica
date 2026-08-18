import { Boton } from './Boton'
import { IconoAnterior, IconoSiguiente } from './iconos'

interface Props {
  pagina: number
  total: number
  tamanioPagina: number
  onCambiarPagina: (pagina: number) => void
  /** Cómo se llaman las filas, para poder decir "20 de 73 choferes" y no "20 de 73 elementos". */
  nombrePlural: string
}

/**
 * El control de paginación del sistema. Reemplaza a las cuatro copias que tenían choferes, flota,
 * viajes y facturación, que eran la misma de 41 a 49 líneas con distinto nombre de parámetro.
 *
 * **El texto no cambia ni una coma**: cuatro tests de la suite lo verifican palabra por palabra
 * (`'Página 2 de 4, mostrando 21 a 40 de 73 choferes'`). Lo mismo el `role="status"`, que es lo que
 * avisa a quien usa lector de pantalla que la lista cambió aunque la pantalla no.
 */
export function Paginacion({
  pagina,
  total,
  tamanioPagina,
  onCambiarPagina,
  nombrePlural,
}: Props) {
  const paginas = Math.max(1, Math.ceil(total / tamanioPagina))
  const desde = total === 0 ? 0 : (pagina - 1) * tamanioPagina + 1
  const hasta = Math.min(pagina * tamanioPagina, total)

  if (total <= tamanioPagina) {
    return null
  }

  return (
    <nav
      aria-label="Paginación"
      className="mt-3 flex flex-wrap items-center justify-between gap-3 rounded-medio border border-borde bg-superficie px-4 py-3"
    >
      <p role="status" className="text-sm text-texto-suave">
        Página {pagina} de {paginas}, mostrando {desde} a {hasta} de {total} {nombrePlural}
      </p>

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
    </nav>
  )
}
