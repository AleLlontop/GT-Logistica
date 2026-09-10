import { Boton } from '../../../compartido/ui/Boton'
import { Filtros, FranjaDeFiltros } from '../../../compartido/ui/Filtros'
import { clasesDeEtiquetaDeFiltro, clasesDeFiltro } from '../../../compartido/ui/clases'
import type { Transportista } from '../../choferes/transportistas/servicioTransportistas'
import type { TipoVehiculo } from '../tiposVehiculo/servicioTiposVehiculo'
import { TEXTO_ESTADO_DOCUMENTACION, TEXTO_FILTRO_ESTADO } from '../servicios/estados'
import type { FiltrosFlota as Filtros_ } from '../servicios/servicioFlota'
import type { ReactNode } from 'react'

interface Props {
  filtros: Filtros_
  transportistas: Transportista[]
  tipos: TipoVehiculo[]
  onCambiar: <C extends keyof Filtros_>(campo: C, valor: Filtros_[C]) => void
  onLimpiar: () => void
  /** La franja de resumen del listado: cantidad y criterio de orden (FR-035). */
  resumen?: ReactNode
}

const ESTADOS_DEL_VEHICULO = ['disponible', 'fueraDeServicio', 'dadoDeBaja'] as const

const ESTADOS_DE_DOCUMENTACION = [
  'enRegla',
  'proximaAvencer',
  'vencida',
  'sinDocumentacion',
] as const

/**
 * Los cuatro filtros del listado, los cuatro por selección exacta entre lo ya cargado (FR-030 del
 * Módulo 4).
 *
 * El de estado del vehículo es un **control único con tres valores excluyentes** (FR-030a). Sus dos
 * valores operativos son complementarios dentro de los activos, y las combinaciones que se pierden
 * —"dados de baja que además estaban disponibles"— no tienen sentido operativo: una unidad fuera de
 * la flota no está disponible para nada.
 *
 * **El control siempre dice qué está filtrando** (FR-037 del Módulo 4): "Todos" significa sólo los
 * activos, y el texto lo aclara. Ninguna fila queda oculta en silencio.
 *
 * **Esta pantalla no tiene búsqueda por texto**: son cuatro desplegables, así que la franja de
 * arriba no se dibuja. **No se le inventa un buscador** que la pantalla no tiene (Principio III).
 * Lo que sí se cumple acá es FR-034: el desplegable que está filtrando se distingue del que no.
 */
export function FiltrosFlota({
  filtros,
  transportistas,
  tipos,
  onCambiar,
  onLimpiar,
  resumen,
}: Props) {
  return (
    <Filtros resumen={resumen}>
      <FranjaDeFiltros>
        <div className="flex flex-col gap-1.5">
          <label htmlFor="filtro-transportista" className={clasesDeEtiquetaDeFiltro}>
            Transportista
          </label>
          <select
            id="filtro-transportista"
            value={filtros.transportistaId}
            onChange={(evento) =>
              onCambiar(
                'transportistaId',
                evento.target.value === '' ? '' : Number(evento.target.value),
              )
            }
            className={clasesDeFiltro(filtros.transportistaId !== '')}
          >
            <option value="">Todos</option>
            {transportistas.map((transportista) => (
              <option key={transportista.id} value={transportista.id}>
                {transportista.nombre}
              </option>
            ))}
          </select>
        </div>

        <div className="flex flex-col gap-1.5">
          <label htmlFor="filtro-tipo" className={clasesDeEtiquetaDeFiltro}>
            Tipo de vehículo
          </label>
          <select
            id="filtro-tipo"
            value={filtros.tipoVehiculoId}
            onChange={(evento) =>
              onCambiar(
                'tipoVehiculoId',
                evento.target.value === '' ? '' : Number(evento.target.value),
              )
            }
            className={clasesDeFiltro(filtros.tipoVehiculoId !== '')}
          >
            <option value="">Todos</option>
            {tipos.map((tipo) => (
              <option key={tipo.id} value={tipo.id}>
                {tipo.nombre}
              </option>
            ))}
          </select>
        </div>

        <div className="flex flex-col gap-1.5">
          <label htmlFor="filtro-estado" className={clasesDeEtiquetaDeFiltro}>
            Estado del vehículo
          </label>
          <select
            id="filtro-estado"
            value={filtros.estado}
            onChange={(evento) => onCambiar('estado', evento.target.value as Filtros_['estado'])}
            aria-describedby="ayuda-filtro-estado"
            className={clasesDeFiltro(filtros.estado !== '')}
          >
            <option value="">Todos</option>
            {ESTADOS_DEL_VEHICULO.map((estado) => (
              <option key={estado} value={estado}>
                {TEXTO_FILTRO_ESTADO[estado]}
              </option>
            ))}
          </select>

          {/* El control dice qué está filtrando. "Todos" no incluye los dados de baja, y
              callárselo haría leer el listado como un error de datos (convención [003]). */}
          {filtros.estado !== '' && (
            <small id="ayuda-filtro-estado" role="status" className="max-w-xs text-[11.5px] text-faint">
              Mostrando sólo: {TEXTO_FILTRO_ESTADO[filtros.estado]}.
            </small>
          )}
        </div>

        <div className="flex flex-col gap-1.5">
          <label htmlFor="filtro-estado-documentacion" className={clasesDeEtiquetaDeFiltro}>
            Estado de documentación
          </label>
          <select
            id="filtro-estado-documentacion"
            value={filtros.estadoDocumentacion}
            onChange={(evento) =>
              onCambiar('estadoDocumentacion', evento.target.value as Filtros_['estadoDocumentacion'])
            }
            aria-describedby="ayuda-filtro-documentacion"
            className={clasesDeFiltro(filtros.estadoDocumentacion !== '')}
          >
            <option value="">Todos</option>
            {ESTADOS_DE_DOCUMENTACION.map((estado) => (
              <option key={estado} value={estado}>
                {TEXTO_ESTADO_DOCUMENTACION[estado]}
              </option>
            ))}
          </select>

          {filtros.estadoDocumentacion !== '' && (
            <small
              id="ayuda-filtro-documentacion"
              role="status"
              className="max-w-xs text-[11.5px] text-faint"
            >
              Mostrando sólo: {TEXTO_ESTADO_DOCUMENTACION[filtros.estadoDocumentacion]}.
            </small>
          )}
        </div>

        <Boton variante="secundario" tamanio="chico" className="self-end" onClick={onLimpiar}>
          Limpiar filtros
        </Boton>
      </FranjaDeFiltros>
    </Filtros>
  )
}
