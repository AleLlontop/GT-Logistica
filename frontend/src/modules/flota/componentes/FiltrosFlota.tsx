import type { Transportista } from '../../choferes/transportistas/servicioTransportistas'
import type { TipoVehiculo } from '../tiposVehiculo/servicioTiposVehiculo'
import { TEXTO_ESTADO_DOCUMENTACION, TEXTO_FILTRO_ESTADO } from '../servicios/estados'
import type { FiltrosFlota as Filtros } from '../servicios/servicioFlota'

interface Props {
  filtros: Filtros
  transportistas: Transportista[]
  tipos: TipoVehiculo[]
  onCambiar: <C extends keyof Filtros>(campo: C, valor: Filtros[C]) => void
  onLimpiar: () => void
}

const ESTADOS_DEL_VEHICULO = ['disponible', 'fueraDeServicio', 'dadoDeBaja'] as const

const ESTADOS_DE_DOCUMENTACION = [
  'enRegla',
  'proximaAvencer',
  'vencida',
  'sinDocumentacion',
] as const

/**
 * Los cuatro filtros del listado, los cuatro por selección exacta entre lo ya cargado (FR-030).
 *
 * El de estado del vehículo es un **control único con tres valores excluyentes** (FR-030a). Sus dos
 * valores operativos son complementarios dentro de los activos, y las combinaciones que se pierden
 * —"dados de baja que además estaban disponibles"— no tienen sentido operativo: una unidad fuera de
 * la flota no está disponible para nada (research §5).
 *
 * **El control siempre dice qué está filtrando** (FR-037): "Todos" significa sólo los activos, y el
 * texto lo aclara. Ninguna fila queda oculta en silencio.
 */
export function FiltrosFlota({ filtros, transportistas, tipos, onCambiar, onLimpiar }: Props) {
  return (
    <section aria-label="Filtros" className="filtros">
      <div className="campo">
        <label htmlFor="filtro-transportista">Transportista</label>
        <select
          id="filtro-transportista"
          value={filtros.transportistaId}
          onChange={(evento) =>
            onCambiar('transportistaId', evento.target.value === '' ? '' : Number(evento.target.value))
          }
        >
          <option value="">Todos</option>
          {transportistas.map((transportista) => (
            <option key={transportista.id} value={transportista.id}>
              {transportista.nombre}
            </option>
          ))}
        </select>
      </div>

      <div className="campo">
        <label htmlFor="filtro-tipo">Tipo de vehículo</label>
        <select
          id="filtro-tipo"
          value={filtros.tipoVehiculoId}
          onChange={(evento) =>
            onCambiar('tipoVehiculoId', evento.target.value === '' ? '' : Number(evento.target.value))
          }
        >
          <option value="">Todos</option>
          {tipos.map((tipo) => (
            <option key={tipo.id} value={tipo.id}>
              {tipo.nombre}
            </option>
          ))}
        </select>
      </div>

      <div className="campo">
        <label htmlFor="filtro-estado">Estado del vehículo</label>
        <select
          id="filtro-estado"
          value={filtros.estado}
          onChange={(evento) => onCambiar('estado', evento.target.value as Filtros['estado'])}
          aria-describedby="ayuda-filtro-estado"
        >
          <option value="">Todos</option>
          {ESTADOS_DEL_VEHICULO.map((estado) => (
            <option key={estado} value={estado}>
              {TEXTO_FILTRO_ESTADO[estado]}
            </option>
          ))}
        </select>

        {/* FR-037: el control dice qué está filtrando. "Todos" no incluye los dados de baja, y
            callárselo haría leer el listado como un error de datos (FR-031). */}
        <small id="ayuda-filtro-estado" role="status">
          {filtros.estado === ''
            ? 'Mostrando sólo las unidades activas. Elegí "Dado de baja" para ver las que salieron de la flota.'
            : `Mostrando sólo: ${TEXTO_FILTRO_ESTADO[filtros.estado]}.`}
        </small>
      </div>

      <div className="campo">
        <label htmlFor="filtro-estado-documentacion">Estado de documentación</label>
        <select
          id="filtro-estado-documentacion"
          value={filtros.estadoDocumentacion}
          onChange={(evento) =>
            onCambiar('estadoDocumentacion', evento.target.value as Filtros['estadoDocumentacion'])
          }
          aria-describedby="ayuda-filtro-documentacion"
        >
          <option value="">Todos</option>
          {ESTADOS_DE_DOCUMENTACION.map((estado) => (
            <option key={estado} value={estado}>
              {TEXTO_ESTADO_DOCUMENTACION[estado]}
            </option>
          ))}
        </select>

        <small id="ayuda-filtro-documentacion" role="status">
          {filtros.estadoDocumentacion === ''
            ? 'Mostrando todos los estados de documentación.'
            : `Mostrando sólo: ${TEXTO_ESTADO_DOCUMENTACION[filtros.estadoDocumentacion]}.`}
        </small>
      </div>

      <button type="button" onClick={onLimpiar}>
        Limpiar filtros
      </button>
    </section>
  )
}
