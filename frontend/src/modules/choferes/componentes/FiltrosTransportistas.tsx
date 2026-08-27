import type { ReactNode } from 'react'
import { Boton } from '../../../compartido/ui/Boton'
import { Filtros, FranjaDeBusqueda, FranjaDeFiltros } from '../../../compartido/ui/Filtros'
import { clasesDeEtiquetaDeFiltro, clasesDeFiltro } from '../../../compartido/ui/clases'
import type { FiltrosDeTransportistas } from '../servicios/formato'
import { FILTROS_TRANSPORTISTAS_VACIOS } from '../servicios/formato'

interface Props {
  valor: FiltrosDeTransportistas
  onCambio: (filtros: FiltrosDeTransportistas) => void
  /** Cantidad y criterio de orden (FR-035). */
  resumen?: ReactNode
}

/**
 * Los filtros del padrón de transportistas.
 *
 * *Nombre o CUIT* es un campo de texto que trae todo lo que **contenga** lo escrito, sin distinguir
 * mayúsculas; el CUIT se normaliza en el servidor, así que buscarlo con guiones también encuentra
 * (FR-025 del Módulo 3).
 *
 * **Pasa a usar la primitiva `Filtros`** (FR-032). Antes repetía a mano el `<section
 * aria-label="Filtros">` con un blob de `[&_input]`, `[&_select]` y `[&_button]` — el mismo
 * antipatrón de [007] que esta feature retira de `EncabezadoDePantalla`. El blob **se borra con
 * ella**, y el buscador sube a la franja de arriba (FR-033).
 */
export function FiltrosTransportistas({ valor, onCambio, resumen }: Props) {
  function actualizar<C extends keyof FiltrosDeTransportistas>(
    campo: C,
    nuevo: FiltrosDeTransportistas[C],
  ) {
    onCambio({ ...valor, [campo]: nuevo })
  }

  return (
    <Filtros resumen={resumen}>
      <FranjaDeBusqueda>
        <div className="flex flex-col gap-1.5">
          <label htmlFor="filtro-texto" className={clasesDeEtiquetaDeFiltro}>
            Nombre o CUIT
          </label>
          <input
            id="filtro-texto"
            type="search"
            placeholder="30-71234567-8"
            value={valor.texto}
            onChange={(evento) => actualizar('texto', evento.target.value)}
            className={clasesDeFiltro(valor.texto.trim() !== '')}
          />
        </div>
      </FranjaDeBusqueda>

      <FranjaDeFiltros>
        <label
          htmlFor="filtro-solo-activos"
          className="flex items-center gap-2 text-[12.5px] font-medium text-ink-soft"
        >
          <input
            id="filtro-solo-activos"
            type="checkbox"
            checked={valor.soloActivos}
            onChange={(evento) => actualizar('soloActivos', evento.target.checked)}
            className="size-4"
          />
          Sólo activos
        </label>

        <Boton
          variante="secundario"
          tamanio="chico"
          onClick={() => onCambio(FILTROS_TRANSPORTISTAS_VACIOS)}
        >
          Limpiar filtros
        </Boton>
      </FranjaDeFiltros>
    </Filtros>
  )
}
