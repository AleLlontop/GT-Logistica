import { useEffect, useState } from 'react'
import {
  listarTransportistas,
  type Transportista,
} from '../../choferes/transportistas/servicioTransportistas'
import { listarClientes, type Cliente } from '../clientes/servicioClientes'
import { NOMBRES_DE_ESTADO, type FiltrosViajes as Filtros } from '../servicios/servicioViajes'

interface Props {
  valor: Filtros
  onCambio: (filtros: Filtros) => void
}

/**
 * Los cuatro filtros del listado más la búsqueda (FR-041, FR-042).
 *
 * **La opción por defecto del estado se llama `Todos menos anulados`**, y ése es todo el punto: sin
 * filtro los anulados no se muestran, y un listado que oculta filas en silencio se lee como un error
 * de datos. El control dice qué está mostrando (FR-044, FR-049, convención [003]).
 *
 * **El filtro por cliente ofrece también los inactivos**, y el de transportista lo mismo: un cliente
 * dado de baja conserva sus viajes históricos, y no poder filtrarlos haría inalcanzable justo la
 * consulta que SC-010 y SC-011 piden.
 */
export function FiltrosViajes({ valor, onCambio }: Props) {
  const [clientes, setClientes] = useState<Cliente[]>([])
  const [transportistas, setTransportistas] = useState<Transportista[]>([])

  useEffect(() => {
    listarClientes({ soloActivos: false, busqueda: '' }, 1)
      .then((pagina) => setClientes(pagina.items))
      .catch(() => setClientes([]))

    listarTransportistas()
      .then(setTransportistas)
      .catch(() => setTransportistas([]))
  }, [])

  function cambiar(parcial: Partial<Filtros>) {
    onCambio({ ...valor, ...parcial })
  }

  return (
    <form onSubmit={(evento) => evento.preventDefault()}>
      <div className="campo">
        <label htmlFor="filtro-cliente">Cliente</label>
        <select
          id="filtro-cliente"
          value={valor.clienteId}
          onChange={(evento) =>
            cambiar({ clienteId: evento.target.value === '' ? '' : Number(evento.target.value) })
          }
        >
          <option value="">Todos los clientes</option>
          {clientes.map((cliente) => (
            <option key={cliente.id} value={cliente.id}>
              {cliente.activo ? cliente.razonSocial : `${cliente.razonSocial} (inactivo)`}
            </option>
          ))}
        </select>
      </div>

      <div className="campo">
        <label htmlFor="filtro-transportista">Transportista</label>
        <select
          id="filtro-transportista"
          value={valor.transportistaId}
          onChange={(evento) =>
            cambiar({
              transportistaId: evento.target.value === '' ? '' : Number(evento.target.value),
            })
          }
        >
          <option value="">Todos los transportistas</option>
          {transportistas.map((transportista) => (
            <option key={transportista.id} value={transportista.id}>
              {transportista.activo
                ? transportista.nombre
                : `${transportista.nombre} (inactivo)`}
            </option>
          ))}
        </select>
      </div>

      <div className="campo">
        <label htmlFor="filtro-estado">Estado</label>
        <select
          id="filtro-estado"
          value={valor.estado}
          onChange={(evento) =>
            cambiar({ estado: evento.target.value as Filtros['estado'] })
          }
        >
          {/* El nombre de la opción por defecto es el requisito: ningún listado oculta filas en
              silencio (FR-044, FR-049). */}
          <option value="">Todos menos anulados</option>
          <option value="pendiente">{NOMBRES_DE_ESTADO.pendiente}</option>
          <option value="enCurso">{NOMBRES_DE_ESTADO.enCurso}</option>
          <option value="rendido">{NOMBRES_DE_ESTADO.rendido}</option>
          {/* Módulo 6, FR-055. Va después de `rendido` porque es el estado que le sigue. */}
          <option value="facturado">{NOMBRES_DE_ESTADO.facturado}</option>
          <option value="anulado">{NOMBRES_DE_ESTADO.anulado}</option>
        </select>
      </div>

      <div className="campo">
        <label htmlFor="filtro-desde">Desde</label>
        <input
          id="filtro-desde"
          type="date"
          value={valor.desde}
          onChange={(evento) => cambiar({ desde: evento.target.value })}
        />
      </div>

      <div className="campo">
        <label htmlFor="filtro-hasta">Hasta</label>
        <input
          id="filtro-hasta"
          type="date"
          value={valor.hasta}
          onChange={(evento) => cambiar({ hasta: evento.target.value })}
        />
      </div>

      <div className="campo">
        <label htmlFor="filtro-busqueda">Buscar por origen, destino o cliente</label>
        <input
          id="filtro-busqueda"
          type="search"
          value={valor.busqueda}
          onChange={(evento) => cambiar({ busqueda: evento.target.value })}
        />
      </div>
    </form>
  )
}
