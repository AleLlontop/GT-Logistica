import { useEffect, useState } from 'react'
import { listarClientes, type Cliente } from '../../viajes/clientes/servicioClientes'
import {
  NOMBRES_DE_ESTADO,
  NOMBRES_DE_TIPO_COMPROBANTE,
  type EstadoFacturaVisible,
  type TipoComprobante,
} from '../servicios/api'
import type { FiltrosFacturas as Filtros } from '../servicios/servicioFacturas'

/** Los años que el sistema acepta hoy. La lista se amplía con el tiempo (FR-010). */
const ANIOS = [2025, 2026]

const MESES = Array.from({ length: 12 }, (_, indice) => indice + 1)

interface Props {
  valor: Filtros
  onCambio: (filtros: Filtros) => void
}

/**
 * Los cinco filtros del listado, todos combinables (FR-058).
 *
 * **La opción por defecto del estado se llama `Todas, incluidas las anuladas`**, y ése es todo el punto:
 * a diferencia del listado de viajes, acá sin filtro **sí** se muestran las anuladas — una factura anulada
 * sigue siendo parte de la historia de cobranza del cliente. El control dice qué está mostrando (FR-064,
 * convención [003]).
 *
 * Sus cuatro valores son **excluyentes**: una factura impaga y pasada de fecha aparece bajo `Vencida` y no
 * bajo `Pendiente` (FR-058a).
 *
 * **El filtro por cliente ofrece también los inactivos**: un cliente dado de baja conserva sus facturas, y
 * no poder filtrarlas haría inalcanzable justo la consulta que US3 esc. 9 describe.
 */
export function FiltrosFacturas({ valor, onCambio }: Props) {
  const [clientes, setClientes] = useState<Cliente[]>([])

  useEffect(() => {
    listarClientes({ soloActivos: false, busqueda: '' }, 1)
      .then((pagina) => setClientes(pagina.items))
      .catch(() => setClientes([]))
  }, [])

  function cambiar(parcial: Partial<Filtros>) {
    onCambio({ ...valor, ...parcial })
  }

  return (
    <form onSubmit={(evento) => evento.preventDefault()} className="flex flex-wrap items-end gap-4 border-b border-borde bg-superficie-hundida px-4 py-3 [&_.campo]:flex [&_.campo]:flex-col [&_.campo]:gap-1 [&_label]:text-xs [&_label]:font-medium [&_label]:text-texto-suave [&_select]:rounded-chico [&_select]:border [&_select]:border-borde-fuerte [&_select]:bg-superficie [&_select]:px-2 [&_select]:py-1.5 [&_select]:text-sm [&_select]:text-texto [&_input]:rounded-chico [&_input]:border [&_input]:border-borde-fuerte [&_input]:bg-superficie [&_input]:px-2 [&_input]:py-1.5 [&_input]:text-sm [&_input]:text-texto [&_button]:rounded-chico [&_button]:border [&_button]:border-borde-fuerte [&_button]:bg-superficie [&_button]:px-3 [&_button]:py-1.5 [&_button]:text-sm">
      <div className="campo">
        <label htmlFor="filtro-cliente-factura">Cliente</label>
        <select
          id="filtro-cliente-factura"
          value={valor.clienteId}
          onChange={(evento) =>
            cambiar({ clienteId: evento.target.value === '' ? '' : Number(evento.target.value) })
          }
        >
          <option value="">Todos los clientes</option>
          {clientes.map((cliente) => (
            <option key={cliente.id} value={cliente.id}>
              {cliente.activo ? cliente.razonSocial : `${cliente.razonSocial} (Inactivo)`}
            </option>
          ))}
        </select>
      </div>

      <div className="campo">
        <label htmlFor="filtro-desde-factura">Desde</label>
        <input
          id="filtro-desde-factura"
          type="date"
          value={valor.desde}
          onChange={(evento) => cambiar({ desde: evento.target.value })}
        />
      </div>

      <div className="campo">
        <label htmlFor="filtro-hasta-factura">Hasta</label>
        <input
          id="filtro-hasta-factura"
          type="date"
          value={valor.hasta}
          onChange={(evento) => cambiar({ hasta: evento.target.value })}
        />
      </div>

      <div className="campo">
        <label htmlFor="filtro-mes">Mes del período</label>
        <select
          id="filtro-mes"
          value={valor.mes}
          onChange={(evento) =>
            cambiar({ mes: evento.target.value === '' ? '' : Number(evento.target.value) })
          }
        >
          <option value="">Todos los meses</option>
          {MESES.map((numero) => (
            <option key={numero} value={numero}>
              {String(numero).padStart(2, '0')}
            </option>
          ))}
        </select>
      </div>

      <div className="campo">
        <label htmlFor="filtro-anio">Año del período</label>
        <select
          id="filtro-anio"
          value={valor.anio}
          onChange={(evento) =>
            cambiar({ anio: evento.target.value === '' ? '' : Number(evento.target.value) })
          }
        >
          <option value="">Todos los años</option>
          {ANIOS.map((numero) => (
            <option key={numero} value={numero}>
              {numero}
            </option>
          ))}
        </select>
      </div>

      <div className="campo">
        <label htmlFor="filtro-estado-factura">Estado</label>
        <select
          id="filtro-estado-factura"
          value={valor.estado}
          onChange={(evento) =>
            cambiar({ estado: evento.target.value as EstadoFacturaVisible | '' })
          }
        >
          {/* El nombre de la opción por defecto es el requisito: ningún listado oculta filas en
              silencio, y acá tampoco esconde las anuladas (FR-064). */}
          <option value="">Todas, incluidas las anuladas</option>
          <option value="pendiente">{NOMBRES_DE_ESTADO.pendiente}</option>
          <option value="vencida">{NOMBRES_DE_ESTADO.vencida}</option>
          <option value="pagada">{NOMBRES_DE_ESTADO.pagada}</option>
          <option value="anulada">{NOMBRES_DE_ESTADO.anulada}</option>
        </select>
      </div>

      <div className="campo">
        <label htmlFor="filtro-tipo-comprobante">Tipo de comprobante</label>
        <select
          id="filtro-tipo-comprobante"
          value={valor.tipoComprobante}
          onChange={(evento) =>
            cambiar({ tipoComprobante: evento.target.value as TipoComprobante | '' })
          }
        >
          <option value="">Todos los tipos</option>
          {Object.entries(NOMBRES_DE_TIPO_COMPROBANTE).map(([clave, nombre]) => (
            <option key={clave} value={clave}>
              {nombre}
            </option>
          ))}
        </select>
      </div>
    </form>
  )
}
