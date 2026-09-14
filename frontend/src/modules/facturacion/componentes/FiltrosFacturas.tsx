import { useEffect, useState, type ReactNode } from 'react'
import { aniosDelPeriodo } from '../../../compartido/fechas'
import { Filtros, FranjaDeFiltros } from '../../../compartido/ui/Filtros'
import { clasesDeEtiquetaDeFiltro, clasesDeFiltro } from '../../../compartido/ui/clases'
import { listarClientes, type Cliente } from '../../viajes/clientes/servicioClientes'
import {
  NOMBRES_DE_ESTADO,
  NOMBRES_DE_TIPO_COMPROBANTE,
  type EstadoFacturaVisible,
  type TipoComprobante,
} from '../servicios/api'
import type { FiltrosFacturas as ValorDeFiltros } from '../servicios/servicioFacturas'

const MESES = Array.from({ length: 12 }, (_, indice) => indice + 1)

interface Props {
  valor: ValorDeFiltros
  onCambio: (filtros: ValorDeFiltros) => void
  /** Lo que declara qué se está mostrando. Un listado nunca oculta filas en silencio ([003]). */
  declaracion?: ReactNode
  /** Cantidad y criterio de orden (FR-035). */
  resumen?: ReactNode
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
 *
 * **Esta pantalla no tiene búsqueda por texto**: son cinco desplegables y dos fechas. La franja de
 * arriba queda vacía y no se dibuja — **no se inventa un buscador** que la pantalla no tiene
 * (Principio III). Lo que sí rige acá es FR-034: el control que está filtrando se distingue del que
 * está en su valor por defecto.
 */
export function FiltrosFacturas({ valor, onCambio, declaracion, resumen }: Props) {
  const [clientes, setClientes] = useState<Cliente[]>([])

  useEffect(() => {
    listarClientes({ soloActivos: false, busqueda: '' }, 1)
      .then((pagina) => setClientes(pagina.items))
      .catch(() => setClientes([]))
  }, [])

  function cambiar(parcial: Partial<ValorDeFiltros>) {
    onCambio({ ...valor, ...parcial })
  }

  return (
    <Filtros declaracion={declaracion} resumen={resumen}>
      <FranjaDeFiltros>
        <div className="flex flex-col gap-1.5">
          <label htmlFor="filtro-cliente-factura" className={clasesDeEtiquetaDeFiltro}>
            Cliente
          </label>
          <select
            id="filtro-cliente-factura"
            value={valor.clienteId}
            onChange={(evento) =>
              cambiar({ clienteId: evento.target.value === '' ? '' : Number(evento.target.value) })
            }
            className={clasesDeFiltro(valor.clienteId !== '')}
          >
            <option value="">Todos los clientes</option>
            {clientes.map((cliente) => (
              <option key={cliente.id} value={cliente.id}>
                {cliente.activo ? cliente.razonSocial : `${cliente.razonSocial} (Inactivo)`}
              </option>
            ))}
          </select>
        </div>

        <div className="flex flex-col gap-1.5">
          <label htmlFor="filtro-desde-factura" className={clasesDeEtiquetaDeFiltro}>
            Desde
          </label>
          <input
            id="filtro-desde-factura"
            type="date"
            value={valor.desde}
            onChange={(evento) => cambiar({ desde: evento.target.value })}
            className={clasesDeFiltro(valor.desde !== '')}
          />
        </div>

        <div className="flex flex-col gap-1.5">
          <label htmlFor="filtro-hasta-factura" className={clasesDeEtiquetaDeFiltro}>
            Hasta
          </label>
          <input
            id="filtro-hasta-factura"
            type="date"
            value={valor.hasta}
            onChange={(evento) => cambiar({ hasta: evento.target.value })}
            className={clasesDeFiltro(valor.hasta !== '')}
          />
        </div>

        <div className="flex flex-col gap-1.5">
          <label htmlFor="filtro-mes" className={clasesDeEtiquetaDeFiltro}>
            Mes del período
          </label>
          <select
            id="filtro-mes"
            value={valor.mes}
            onChange={(evento) =>
              cambiar({ mes: evento.target.value === '' ? '' : Number(evento.target.value) })
            }
            className={clasesDeFiltro(valor.mes !== '')}
          >
            <option value="">Todos los meses</option>
            {MESES.map((numero) => (
              <option key={numero} value={numero}>
                {String(numero).padStart(2, '0')}
              </option>
            ))}
          </select>
        </div>

        <div className="flex flex-col gap-1.5">
          <label htmlFor="filtro-anio" className={clasesDeEtiquetaDeFiltro}>
            Año del período
          </label>
          <select
            id="filtro-anio"
            value={valor.anio}
            onChange={(evento) =>
              cambiar({ anio: evento.target.value === '' ? '' : Number(evento.target.value) })
            }
            className={clasesDeFiltro(valor.anio !== '')}
          >
            <option value="">Todos los años</option>
            {aniosDelPeriodo().map((numero) => (
              <option key={numero} value={numero}>
                {numero}
              </option>
            ))}
          </select>
        </div>

        <div className="flex flex-col gap-1.5">
          <label htmlFor="filtro-estado-factura" className={clasesDeEtiquetaDeFiltro}>
            Estado
          </label>
          <select
            id="filtro-estado-factura"
            value={valor.estado}
            onChange={(evento) => cambiar({ estado: evento.target.value as EstadoFacturaVisible | '' })}
            className={clasesDeFiltro(valor.estado !== '')}
          >
            {/* El nombre de la opción por defecto es el requisito: ningún listado oculta filas en
                silencio, y acá tampoco esconde las anuladas (FR-064 del Módulo 6). */}
            <option value="">Todas, incluidas las anuladas</option>
            <option value="pendiente">{NOMBRES_DE_ESTADO.pendiente}</option>
            <option value="vencida">{NOMBRES_DE_ESTADO.vencida}</option>
            <option value="pagada">{NOMBRES_DE_ESTADO.pagada}</option>
            <option value="anulada">{NOMBRES_DE_ESTADO.anulada}</option>
          </select>
        </div>

        <div className="flex flex-col gap-1.5">
          <label htmlFor="filtro-tipo-comprobante" className={clasesDeEtiquetaDeFiltro}>
            Tipo de comprobante
          </label>
          <select
            id="filtro-tipo-comprobante"
            value={valor.tipoComprobante}
            onChange={(evento) =>
              cambiar({ tipoComprobante: evento.target.value as TipoComprobante | '' })
            }
            className={clasesDeFiltro(valor.tipoComprobante !== '')}
          >
            <option value="">Todos los tipos</option>
            {Object.entries(NOMBRES_DE_TIPO_COMPROBANTE).map(([clave, nombre]) => (
              <option key={clave} value={clave}>
                {nombre}
              </option>
            ))}
          </select>
        </div>
      </FranjaDeFiltros>
    </Filtros>
  )
}
