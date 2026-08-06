interface Props {
  pagina: number
  total: number
  tamanioPagina: number
  onCambiarPagina: (pagina: number) => void
  /** Cómo se llaman las filas, para poder decir "20 de 73 choferes" y no "20 de 73 elementos". */
  nombrePlural: string
}

/**
 * Control de paginación (FR-030).
 *
 * Muestra el total de coincidencias, no sólo las de la página: es lo que permite saber cuántos
 * choferes cumplen el filtro sin ir a la última página a contar.
 *
 * El cambio de página se anuncia con `role="status"` para que no sea sólo un cambio visual de la
 * tabla: quien usa lector de pantalla necesita enterarse de que la lista cambió.
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
    <nav aria-label="Paginación" className="paginacion">
      <p role="status">
        Página {pagina} de {paginas}, mostrando {desde} a {hasta} de {total} {nombrePlural}
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
