import { Listado, TablaDesplazable } from '../../../compartido/ui/Listado'
import { SeccionNumerada } from '../../../compartido/ui/SeccionNumerada'
import { formatearFecha } from '../../../compartido/fechas'
import { formatearPesos } from '../../../compartido/moneda'
import type { ViajeFacturable } from '../servicios/servicioFacturas'

/** La leyenda exacta que acompaña a un viaje sin remito (contracts/README §Bloque 2). */
export const LEYENDA_SIN_REMITO = 'Sin remito — no se puede facturar'

interface Props {
  /** Su lugar en el orden de lectura del alta. Es una franja más del formulario, no un bloque suelto. */
  numero: number
  viajes: ViajeFacturable[]
  seleccionados: Set<number>
  /** Nombre del cliente y período elegidos, para el mensaje de lista vacía (FR-021). */
  cliente: string
  mes: string
  anio: string
  cargando: boolean
  onCambiarSeleccion: (seleccionados: Set<number>) => void
}

/**
 * Bloque 2 del alta: los viajes facturables, con una casilla para incluir cada uno (FR-019).
 *
 * **Un viaje sin remito aparece igual, con la casilla deshabilitada y la leyenda al lado** (FR-019a).
 * Esconderlo dejaría a quien opera buscando un viaje que sabe que existe y que la pantalla no muestra,
 * sin ninguna pista de por qué. Un listado no oculta filas en silencio y tampoco las ofrece sin decir
 * lo que sabe de ellas (convención [003]).
 *
 * **Sin viajes facturables el mensaje nombra la combinación** —cliente, mes y año— y explica el criterio
 * en vez de mostrar una tabla vacía (FR-021).
 */
export function SelectorDeViajes({
  numero,
  viajes,
  seleccionados,
  cliente,
  mes,
  anio,
  cargando,
  onCambiarSeleccion,
}: Props) {
  function alternar(id: number) {
    const nuevos = new Set(seleccionados)

    if (nuevos.has(id)) {
      nuevos.delete(id)
    } else {
      nuevos.add(id)
    }

    onCambiarSeleccion(nuevos)
  }

  if (cargando) {
    return (
      <SeccionNumerada numero={numero} titulo="Viajes a facturar">
        <p role="status" className="m-0 text-[13px] text-ink-soft">
          Buscando viajes facturables…
        </p>
      </SeccionNumerada>
    )
  }

  if (viajes.length === 0) {
    return (
      <SeccionNumerada numero={numero} titulo="Viajes a facturar">
        <p role="status" className="m-0 text-[13px] leading-5 text-ink-soft">
          No hay viajes facturables de {cliente} en {mes} de {anio}. Se ofrecen sólo los viajes
          rendidos, sin facturar, cuya fecha cae en ese período.
        </p>
      </SeccionNumerada>
    )
  }

  return (
    <SeccionNumerada numero={numero} titulo="Viajes a facturar">
      <Listado>
        <TablaDesplazable>
          <table>
            <caption>Viajes rendidos sin facturar del período</caption>
            <thead>
              <tr>
                <th scope="col">Incluir</th>
                <th scope="col">Número</th>
                <th scope="col">Fecha</th>
                <th scope="col">Remito</th>
                {/* `Origen` + `Destino` nombran un solo concepto: la ruta (FR-047). */}
                <th scope="col">Ruta</th>
                <th scope="col" className="text-right">
                  Importe
                </th>
              </tr>
            </thead>
            <tbody>
              {viajes.map((viaje) => (
                <tr key={viaje.id}>
                  <td>
                    <input
                      type="checkbox"
                      id={`viaje-${viaje.id}`}
                      checked={seleccionados.has(viaje.id)}
                      disabled={!viaje.puedeFacturarse}
                      onChange={() => alternar(viaje.id)}
                      aria-label={`Incluir el viaje ${viaje.numero}`}
                    />
                  </td>
                  <td>
                    {/* **Sin enlace de fila ni chevron**: sus filas llevan casilla, y el clic sobre la
                        casilla no navega (data-model §5). El número queda como etiqueta de la casilla. */}
                    <label htmlFor={`viaje-${viaje.id}`} className="font-mono font-semibold">
                      {viaje.numero}
                    </label>
                  </td>
                  <td>{formatearFecha(viaje.fecha)}</td>
                  <td>
                    {/* La palabra que lo explica, no sólo la casilla apagada: un elemento atenuado lleva
                        además el texto que dice por qué (FR-065, convención [003]). */}
                    {viaje.puedeFacturarse ? (
                      <span className="font-mono">{viaje.numeroRemito}</span>
                    ) : (
                      <span className="atenuada">{LEYENDA_SIN_REMITO}</span>
                    )}
                  </td>
                  <td className="whitespace-nowrap">
                    {viaje.origen} <span className="text-dim">→</span> {viaje.destino}
                  </td>
                  <td className="text-right font-bold whitespace-nowrap">
                    {formatearPesos(viaje.importe)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </TablaDesplazable>
      </Listado>
    </SeccionNumerada>
  )
}
