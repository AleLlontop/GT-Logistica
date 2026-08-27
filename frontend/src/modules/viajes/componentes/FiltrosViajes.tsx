import { useEffect, useState, type ReactNode } from 'react'
import { Filtros as ContenedorDeFiltros, FranjaDeBusqueda, FranjaDeFiltros } from '../../../compartido/ui/Filtros'
import { clasesDeEtiquetaDeFiltro, clasesDeFiltro } from '../../../compartido/ui/clases'
import {
  listarTransportistas,
  type Transportista,
} from '../../choferes/transportistas/servicioTransportistas'
import { listarClientes, type Cliente } from '../clientes/servicioClientes'
import { NOMBRES_DE_ESTADO, type FiltrosViajes as Filtros } from '../servicios/servicioViajes'

interface Props {
  valor: Filtros
  onCambio: (filtros: Filtros) => void
  /** Lo que declara qué se está mostrando. Un listado nunca oculta filas en silencio ([003]). */
  declaracion?: ReactNode
  /** Cantidad, total del período y criterio de orden (FR-035). */
  resumen?: ReactNode
}

/**
 * Los cuatro filtros del listado más la búsqueda (FR-041, FR-042 del Módulo 5).
 *
 * **El buscador sube a la primera franja y va a todo el ancho** (FR-033). Estaba **último**, después
 * de tres desplegables y dos fechas: el control con el que más se entra a la pantalla era el que
 * había que ir a buscar. Los demás quedan debajo como desplegables compactos, y **el que está
 * filtrando se distingue** por borde y fondo propios (FR-034).
 *
 * **La opción por defecto del estado se llama `Todos menos anulados`**, y ése es todo el punto: sin
 * filtro los anulados no se muestran, y un listado que oculta filas en silencio se lee como un error
 * de datos. El control dice qué está mostrando (convención [003]).
 *
 * **El filtro por cliente ofrece también los inactivos**, y el de transportista lo mismo: un cliente
 * dado de baja conserva sus viajes históricos, y no poder filtrarlos haría inalcanzable justo la
 * consulta que el Módulo 5 pedía.
 */
export function FiltrosViajes({ valor, onCambio, declaracion, resumen }: Props) {
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
    <ContenedorDeFiltros declaracion={declaracion} resumen={resumen}>
      <FranjaDeBusqueda>
        <div className="flex flex-col gap-1.5">
          <label htmlFor="filtro-busqueda" className={clasesDeEtiquetaDeFiltro}>
            Buscar por origen, destino o cliente
          </label>
          <input
            id="filtro-busqueda"
            type="search"
            placeholder="Rosario"
            value={valor.busqueda}
            onChange={(evento) => cambiar({ busqueda: evento.target.value })}
            className={clasesDeFiltro(valor.busqueda.trim() !== '')}
          />
        </div>
      </FranjaDeBusqueda>

      <FranjaDeFiltros>
        <div className="flex flex-col gap-1.5">
          <label htmlFor="filtro-cliente" className={clasesDeEtiquetaDeFiltro}>
            Cliente
          </label>
          <select
            id="filtro-cliente"
            value={valor.clienteId}
            onChange={(evento) =>
              cambiar({ clienteId: evento.target.value === '' ? '' : Number(evento.target.value) })
            }
            className={clasesDeFiltro(valor.clienteId !== '')}
          >
            <option value="">Todos los clientes</option>
            {clientes.map((cliente) => (
              <option key={cliente.id} value={cliente.id}>
                {cliente.activo ? cliente.razonSocial : `${cliente.razonSocial} (inactivo)`}
              </option>
            ))}
          </select>
        </div>

        <div className="flex flex-col gap-1.5">
          <label htmlFor="filtro-transportista" className={clasesDeEtiquetaDeFiltro}>
            Transportista
          </label>
          <select
            id="filtro-transportista"
            value={valor.transportistaId}
            onChange={(evento) =>
              cambiar({
                transportistaId: evento.target.value === '' ? '' : Number(evento.target.value),
              })
            }
            className={clasesDeFiltro(valor.transportistaId !== '')}
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

        <div className="flex flex-col gap-1.5">
          <label htmlFor="filtro-estado" className={clasesDeEtiquetaDeFiltro}>
            Estado
          </label>
          <select
            id="filtro-estado"
            value={valor.estado}
            onChange={(evento) => cambiar({ estado: evento.target.value as Filtros['estado'] })}
            className={clasesDeFiltro(valor.estado !== '')}
          >
            {/* El nombre de la opción por defecto es el requisito: ningún listado oculta filas en
                silencio (convención [003]). */}
            <option value="">Todos menos anulados</option>
            <option value="pendiente">{NOMBRES_DE_ESTADO.pendiente}</option>
            <option value="enCurso">{NOMBRES_DE_ESTADO.enCurso}</option>
            <option value="rendido">{NOMBRES_DE_ESTADO.rendido}</option>
            {/* Módulo 6, FR-055. Va después de `rendido` porque es el estado que le sigue. */}
            <option value="facturado">{NOMBRES_DE_ESTADO.facturado}</option>
            <option value="anulado">{NOMBRES_DE_ESTADO.anulado}</option>
          </select>
        </div>

        <div className="flex flex-col gap-1.5">
          <label htmlFor="filtro-desde" className={clasesDeEtiquetaDeFiltro}>
            Desde
          </label>
          <input
            id="filtro-desde"
            type="date"
            value={valor.desde}
            onChange={(evento) => cambiar({ desde: evento.target.value })}
            className={clasesDeFiltro(valor.desde !== '')}
          />
        </div>

        <div className="flex flex-col gap-1.5">
          <label htmlFor="filtro-hasta" className={clasesDeEtiquetaDeFiltro}>
            Hasta
          </label>
          <input
            id="filtro-hasta"
            type="date"
            value={valor.hasta}
            onChange={(evento) => cambiar({ hasta: evento.target.value })}
            className={clasesDeFiltro(valor.hasta !== '')}
          />
        </div>
      </FranjaDeFiltros>
    </ContenedorDeFiltros>
  )
}
