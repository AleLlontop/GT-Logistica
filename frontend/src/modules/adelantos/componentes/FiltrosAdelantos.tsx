import { useEffect, useState, type ReactNode } from 'react'
import { Filtros, FranjaDeFiltros } from '../../../compartido/ui/Filtros'
import { clasesDeEtiquetaDeFiltro, clasesDeFiltro } from '../../../compartido/ui/clases'
import {
  FILTROS_ADELANTOS_INICIALES,
  formatearPersona,
  listarPersonasConAdelantos,
  NOMBRES_DE_ESTADO,
  rangoInvertido,
  type EstadoAdelanto,
  type FiltrosAdelantos as ValorDeFiltros,
  type PersonaResumen,
} from '../servicios/servicioAdelantos'
import { Boton } from '../../../compartido/ui/Boton'

/** Un filtro en error: el mismo control, con el borde y el fondo del error (FR-015). */
const EN_ERROR = 'border-danger bg-danger-bg'

interface Props {
  valor: ValorDeFiltros
  onCambio: (filtros: ValorDeFiltros) => void
  /** Lo que declara qué se está mostrando. Un listado nunca oculta filas en silencio ([003]). */
  declaracion?: ReactNode
  /** Cantidad, total adelantado y criterio de orden. */
  resumen?: ReactNode
}

/**
 * Los cuatro filtros del listado, combinables (FR-014).
 *
 * **El de persona ofrece sólo a quienes tienen algún adelanto**, activas o dadas de baja: elegir a alguien
 * sin adelantos siempre daría un listado vacío, y dejar afuera a un dado de baja haría inalcanzable la
 * consulta de lo que se le adelantó.
 *
 * **El rango invertido marca los dos campos** y dice cómo corregirlo; la pantalla no consulta mientras
 * tanto (FR-015).
 */
export function FiltrosAdelantos({ valor, onCambio, declaracion, resumen }: Props) {
  const [personas, setPersonas] = useState<PersonaResumen[]>([])

  useEffect(() => {
    listarPersonasConAdelantos()
      .then(setPersonas)
      .catch(() => setPersonas([]))
  }, [])

  function cambiar(parcial: Partial<ValorDeFiltros>) {
    onCambio({ ...valor, ...parcial })
  }

  const invertido = rangoInvertido(valor)

  return (
    <Filtros declaracion={declaracion} resumen={resumen}>
      <FranjaDeFiltros>
        <div className="flex flex-col gap-1.5">
          <label htmlFor="filtro-persona-adelanto" className={clasesDeEtiquetaDeFiltro}>
            Persona
          </label>
          <select
            id="filtro-persona-adelanto"
            value={valor.personaId}
            onChange={(evento) =>
              cambiar({ personaId: evento.target.value === '' ? '' : Number(evento.target.value) })
            }
            className={clasesDeFiltro(valor.personaId !== '')}
          >
            <option value="">Todas las personas</option>
            {personas.map((persona) => (
              <option key={persona.id} value={persona.id}>
                {formatearPersona(persona)} — {persona.dni}
              </option>
            ))}
          </select>
        </div>

        <div className="flex flex-col gap-1.5">
          <label htmlFor="filtro-desde-adelanto" className={clasesDeEtiquetaDeFiltro}>
            Desde
          </label>
          <input
            id="filtro-desde-adelanto"
            type="date"
            value={valor.desde}
            onChange={(evento) => cambiar({ desde: evento.target.value })}
            aria-invalid={invertido}
            aria-describedby={invertido ? 'error-rango-adelantos' : undefined}
            className={clasesDeFiltro(valor.desde !== '', invertido ? EN_ERROR : undefined)}
          />
        </div>

        <div className="flex flex-col gap-1.5">
          <label htmlFor="filtro-hasta-adelanto" className={clasesDeEtiquetaDeFiltro}>
            Hasta
          </label>
          <input
            id="filtro-hasta-adelanto"
            type="date"
            value={valor.hasta}
            onChange={(evento) => cambiar({ hasta: evento.target.value })}
            aria-invalid={invertido}
            aria-describedby={invertido ? 'error-rango-adelantos' : undefined}
            className={clasesDeFiltro(valor.hasta !== '', invertido ? EN_ERROR : undefined)}
          />
          {invertido && (
            <p id="error-rango-adelantos" className="m-0 text-[11.5px] font-medium text-danger-text">
              La fecha desde es posterior a la hasta. Corregí una de las dos.
            </p>
          )}
        </div>

        <div className="flex flex-col gap-1.5">
          <label htmlFor="filtro-estado-adelanto" className={clasesDeEtiquetaDeFiltro}>
            Estado
          </label>
          <select
            id="filtro-estado-adelanto"
            value={valor.estado}
            onChange={(evento) => cambiar({ estado: evento.target.value as EstadoAdelanto | '' })}
            className={clasesDeFiltro(valor.estado !== '')}
          >
            <option value="">Todos los estados</option>
            <option value="pendiente">{NOMBRES_DE_ESTADO.pendiente}</option>
            <option value="aprobado">{NOMBRES_DE_ESTADO.aprobado}</option>
            <option value="rechazado">{NOMBRES_DE_ESTADO.rechazado}</option>
            <option value="anulado">{NOMBRES_DE_ESTADO.anulado}</option>
          </select>
        </div>

        <div className="ml-auto flex flex-col gap-1.5">
          <label className={clasesDeEtiquetaDeFiltro + ' invisible'} aria-hidden="true">
            Limpiar
          </label>
          <Boton
            variante="secundario"
            tamanio="chico"
            onClick={() => onCambio(FILTROS_ADELANTOS_INICIALES)}
          >
            Limpiar filtros
          </Boton>
        </div>
      </FranjaDeFiltros>
    </Filtros>
  )
}
