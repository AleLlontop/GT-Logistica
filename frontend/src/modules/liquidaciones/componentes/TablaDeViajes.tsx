import { Boton } from '../../../compartido/ui/Boton'
import { Estado } from '../../../compartido/ui/Estado'
import { Listado, TablaDesplazable } from '../../../compartido/ui/Listado'
import { TokenDeIdentificador } from '../../../compartido/ui/TokenDeIdentificador'
import { formatearFecha } from '../../../compartido/fechas'
import { formatearPesos } from '../../../compartido/moneda'
import { cantidadDeViajes } from '../servicios/servicioLiquidaciones'

export interface FilaDeViaje {
  id: number
  numero: number
  fecha: string
  origen: string
  destino: string
  importe: number
  estado?: 'rendido' | 'facturado'
}

interface Props {
  viajes: FilaDeViaje[]
  /** El `caption` de la tabla: lo lee un lector de pantalla al entrar. */
  titulo: string
  /** La columna *Estado*, que la generación muestra y el detalle no. */
  conEstado?: boolean
  /** El token enlaza a la ficha del viaje, como en el detalle. */
  conEnlace?: boolean
  /**
   * Una acción por fila —*Quitar*, *Agregar*— **siempre en el DOM** y con nombre accesible que nombra el
   * viaje: un control que aparece al pasar el mouse es inalcanzable con teclado (convención [008]).
   */
  accion?: { etiqueta: 'Quitar' | 'Agregar'; onAccion: (viaje: FilaDeViaje) => void }
  /** Sin pie de total: la lista de disponibles de la edición no suma a nada. */
  sinTotal?: boolean
  className?: string
}

/**
 * La tabla de viajes de una liquidación: la usan la generación, el detalle y la edición, con la misma
 * columna de importe y el mismo pie de total (plan §Structure Decision).
 *
 * Origen y destino van en una sola columna *Ruta*: son un solo concepto.
 */
export function TablaDeViajes({
  viajes,
  titulo,
  conEstado = false,
  conEnlace = false,
  accion,
  sinTotal = false,
  className,
}: Props) {
  const total = viajes.reduce((suma, viaje) => suma + viaje.importe, 0)

  // Viaje, Fecha, Ruta y, opcionalmente, Estado: las columnas que van antes del importe.
  const columnasAntesDelImporte = conEstado ? 4 : 3

  return (
    <Listado className={className}>
      <TablaDesplazable>
        <table>
          <caption>{titulo}</caption>
          <thead>
            <tr>
              <th scope="col">Viaje</th>
              <th scope="col">Fecha</th>
              <th scope="col">Ruta</th>
              {conEstado && <th scope="col">Estado</th>}
              <th scope="col" className="text-right">
                Importe
              </th>
              {accion !== undefined && (
                <th scope="col" className="w-24">
                  <span className="sr-only">Acción</span>
                </th>
              )}
            </tr>
          </thead>
          <tbody>
            {viajes.map((viaje) => (
              <tr key={viaje.id}>
                <td>
                  <TokenDeIdentificador
                    a={conEnlace ? `/viajes/${viaje.id}` : undefined}
                    numero={viaje.numero}
                  />
                </td>
                <td className="whitespace-nowrap">{formatearFecha(viaje.fecha)}</td>
                <td className="whitespace-nowrap">
                  {viaje.origen}{' '}
                  <span aria-hidden="true" className="text-dim">
                    →
                  </span>{' '}
                  {viaje.destino}
                </td>
                {conEstado && (
                  <td>
                    {viaje.estado !== undefined && (
                      <Estado
                        valor={viaje.estado}
                        texto={viaje.estado === 'facturado' ? 'Facturado' : 'Rendido'}
                        forma="pastilla"
                      />
                    )}
                  </td>
                )}
                <td className="text-right font-bold whitespace-nowrap">
                  {formatearPesos(viaje.importe)}
                </td>
                {accion !== undefined && (
                  <td className="text-right">
                    <Boton
                      variante="secundario"
                      tamanio="chico"
                      aria-label={`${accion.etiqueta} viaje #${viaje.numero}`}
                      onClick={() => accion.onAccion(viaje)}
                    >
                      {accion.etiqueta}
                    </Boton>
                  </td>
                )}
              </tr>
            ))}
          </tbody>
          {!sinTotal && (
            <tfoot>
              <tr>
                <td colSpan={columnasAntesDelImporte}>
                  <span className="flex flex-wrap items-center justify-between gap-3">
                    <span className="font-medium text-ink-soft">{cantidadDeViajes(viajes.length)}</span>
                    <span>Importe total</span>
                  </span>
                </td>
                <td className="text-right whitespace-nowrap">{formatearPesos(total)}</td>
                {accion !== undefined && <td />}
              </tr>
            </tfoot>
          )}
        </table>
      </TablaDesplazable>
    </Listado>
  )
}
