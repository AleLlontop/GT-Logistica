interface Props {
  pagina: number
  total: number
  tamanioPagina: number
  /** Qué se está paginando, para que el anuncio diga "de 73 viajes" y no "de 73 elementos". */
  entidad: string
  onCambiarPagina: (pagina: number) => void
}

/**
 * Control de paginación del módulo (FR-043), con la misma forma que el del Módulo 4.
 *
 * Muestra el **total de coincidencias**, no las de la página: es lo que permite saber cuántos viajes
 * cumplen el filtro sin ir a la última página a contar.
 *
 * El cambio de página se anuncia con `role="status"` (convención [003]): es un resultado que aparece
 * sin que la pantalla cambie, y quien usa lector de pantalla necesita enterarse de que la lista se
 * renovó.
 */
export function Paginacion({ pagina, total, tamanioPagina, entidad, onCambiarPagina }: Props) {
  const paginas = Math.max(1, Math.ceil(total / tamanioPagina))
  const desde = total === 0 ? 0 : (pagina - 1) * tamanioPagina + 1
  const hasta = Math.min(pagina * tamanioPagina, total)

  if (total <= tamanioPagina) {
    return null
  }

  return (
    <nav aria-label="Paginación" className="paginacion">
      <p role="status">
        Página {pagina} de {paginas}, mostrando {desde} a {hasta} de {total} {entidad}
      </p>

      <button type="button" onClick={() => onCambiarPagina(pagina - 1)} disabled={pagina <= 1}>
        Anterior
      </button>

      <button type="button" onClick={() => onCambiarPagina(pagina + 1)} disabled={pagina >= paginas}>
        Siguiente
      </button>
    </nav>
  )
}
