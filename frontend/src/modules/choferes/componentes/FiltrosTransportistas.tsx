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
    <section aria-label="Filtros" className="flex flex-wrap items-end gap-4 border-b border-borde bg-superficie-hundida px-4 py-3 [&_.campo]:flex [&_.campo]:flex-col [&_.campo]:gap-1 [&_label]:text-xs [&_label]:font-medium [&_label]:text-texto-suave [&_select]:rounded-chico [&_select]:border [&_select]:border-borde-fuerte [&_select]:bg-superficie [&_select]:px-2 [&_select]:py-1.5 [&_select]:text-sm [&_select]:text-texto [&_input]:rounded-chico [&_input]:border [&_input]:border-borde-fuerte [&_input]:bg-superficie [&_input]:px-2 [&_input]:py-1.5 [&_input]:text-sm [&_input]:text-texto [&_button]:rounded-chico [&_button]:border [&_button]:border-borde-fuerte [&_button]:bg-superficie [&_button]:px-3 [&_button]:py-1.5 [&_button]:text-sm">
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
