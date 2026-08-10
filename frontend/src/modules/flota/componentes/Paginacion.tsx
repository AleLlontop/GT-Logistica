interface Props {
  pagina: number
  total: number
  tamanioPagina: number
  onCambiarPagina: (pagina: number) => void
}

/**
 * Control de paginación del módulo (FR-032).
 *
 * Muestra el total de coincidencias, no sólo las de la página: es lo que permite saber cuántas
 * unidades cumplen el filtro sin ir a la última página a contar.
 *
 * El cambio de página se anuncia con `role="status"` para que no sea sólo un cambio visual de la
 * tabla: quien usa lector de pantalla necesita enterarse de que la lista cambió.
 */
export function Paginacion({ pagina, total, tamanioPagina, onCambiarPagina }: Props) {
  const paginas = Math.max(1, Math.ceil(total / tamanioPagina))
  const desde = total === 0 ? 0 : (pagina - 1) * tamanioPagina + 1
  const hasta = Math.min(pagina * tamanioPagina, total)

  if (total <= tamanioPagina) {
    return null
  }

  return (
    <nav aria-label="Paginación" className="paginacion">
      <p role="status">
        Página {pagina} de {paginas}, mostrando {desde} a {hasta} de {total} vehículos
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
