import { formatearInstante } from '../../../compartido/fechas'
import { formatearPesos } from '../../../compartido/moneda'
import { TablaDesplazable } from '../../../compartido/ui/Listado'
import { NOMBRES_DE_TIPO, type MovimientoListado } from '../servicios/servicioCaja'

interface Props {
  /** El `caption` de la tabla: cada pantalla dice de qué movimientos se trata. */
  titulo: string
  movimientos: MovimientoListado[]
}

/**
 * Las seis columnas de CA5 —fecha, tipo, importe, concepto, responsable y referencia—, iguales en la caja,
 * en el resumen de cierre y en la consulta global. Un movimiento no se abre ni se edita: las filas no
 * navegan (FR-014).
 */
export function TablaDeMovimientos({ titulo, movimientos }: Props) {
  return (
    <TablaDesplazable>
      <table>
        <caption>{titulo}</caption>
        <thead>
          <tr>
            <th scope="col">Fecha</th>
            <th scope="col">Tipo</th>
            <th scope="col" className="text-right">
              Importe
            </th>
            <th scope="col">Concepto</th>
            <th scope="col">Responsable</th>
            <th scope="col">Referencia</th>
          </tr>
        </thead>
        <tbody>
          {movimientos.map((movimiento) => (
            <tr key={movimiento.id}>
              <td className="whitespace-nowrap">{formatearInstante(movimiento.fecha)}</td>
              <td>{NOMBRES_DE_TIPO[movimiento.tipo]}</td>
              <td className="text-right font-bold whitespace-nowrap">{formatearPesos(movimiento.importe)}</td>
              <td>
                <span className="block max-w-[18rem] truncate">{movimiento.concepto}</span>
              </td>
              <td>{movimiento.responsable.nombre}</td>
              <td className="whitespace-nowrap">{movimiento.referencia ?? ''}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </TablaDesplazable>
  )
}
