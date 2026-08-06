import type { FiltrosDeTransportistas } from '../servicios/formato'
import { FILTROS_TRANSPORTISTAS_VACIOS } from '../servicios/formato'

interface Props {
  valor: FiltrosDeTransportistas
  onCambio: (filtros: FiltrosDeTransportistas) => void
}

/**
 * Los filtros del padrón de transportistas.
 *
 * *Nombre o CUIT* es un campo de texto que trae todo lo que **contenga** lo escrito, sin distinguir
 * mayúsculas; el CUIT se normaliza en el servidor, así que buscarlo con guiones también encuentra
 * (FR-025).
 */
export function FiltrosTransportistas({ valor, onCambio }: Props) {
  function actualizar<C extends keyof FiltrosDeTransportistas>(
    campo: C,
    nuevo: FiltrosDeTransportistas[C],
  ) {
    onCambio({ ...valor, [campo]: nuevo })
  }

  return (
    <section aria-label="Filtros" className="filtros">
      <div className="campo">
        <label htmlFor="filtro-texto">Nombre o CUIT</label>
        <input
          id="filtro-texto"
          type="search"
          value={valor.texto}
          onChange={(evento) => actualizar('texto', evento.target.value)}
        />
      </div>

      <div className="campo-checkbox">
        <input
          id="filtro-solo-activos"
          type="checkbox"
          checked={valor.soloActivos}
          onChange={(evento) => actualizar('soloActivos', evento.target.checked)}
        />
        <label htmlFor="filtro-solo-activos">Sólo activos</label>
      </div>

      <button type="button" onClick={() => onCambio(FILTROS_TRANSPORTISTAS_VACIOS)}>
        Limpiar filtros
      </button>
    </section>
  )
}
