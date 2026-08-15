import { formatearPesos } from '../../../compartido/moneda'
import type { TipoComprobante } from '../servicios/api'

/**
 * Las alícuotas de FR-023, escritas también acá.
 *
 * **Es la única regla del módulo que se duplica en el frontend**, y vale decir por qué: el bloque de
 * importes se actualiza en cada tilde y en cada cambio de tipo (FR-020, FR-025), y pedirle al servidor
 * un recálculo por cada clic sería ruido. Lo que **no** se duplica es de dónde sale el valor que se
 * guarda: el que persiste es siempre el que calcula el backend a partir de los viajes que encontró en
 * la base (FR-024, research §9).
 */
const ALICUOTAS: Record<TipoComprobante, number> = {
  facturaA: 0.21,
  facturaB: 0.21,
  facturaC: 0,
}

interface Props {
  /** Los importes de los viajes seleccionados, en el orden que sea: se suman. */
  importes: number[]
  tipoComprobante: TipoComprobante
}

/**
 * Bloque 3 del alta: los tres importes, recalculados en cada cambio (FR-020, FR-025).
 *
 * **Los tres son de sólo lectura y no hay ningún campo donde escribirlos** (FR-024). No están
 * deshabilitados: no existen como campos. Es la diferencia entre "no podés editarlo" y "no es algo que
 * se edite".
 *
 * Con `Factura C` el IVA muestra `(0%)` y `$ 0,00`, y el total es igual al neto: **no es un error ni una
 * factura incompleta** (FR-023).
 *
 * Los importes se formatean con `formatearPesos`, nunca con `toFixed(2)`: eso deja los miles sin separar
 * y el decimal con punto, que en una planilla argentina se lee como separador de miles (convención [005]).
 */
export function ResumenDeImportes({ importes, tipoComprobante }: Props) {
  const alicuota = ALICUOTAS[tipoComprobante]

  // Redondeo comercial a dos decimales, el mismo criterio del backend: la mitad para arriba.
  const neto = importes.reduce((suma, importe) => suma + importe, 0)
  const iva = Math.round(neto * alicuota * 100) / 100
  const total = neto + iva

  const porcentaje = Math.round(alicuota * 100)

  return (
    <section aria-labelledby="titulo-importes">
      <h2 id="titulo-importes">Importes</h2>

      {/* Se actualiza sin que la pantalla cambie, así que se anuncia (convención [003]). */}
      <p role="status">
        <strong>
          {importes.length} {importes.length === 1 ? 'viaje seleccionado' : 'viajes seleccionados'}
        </strong>
      </p>

      <dl>
        <div>
          <dt>Neto</dt>
          <dd>{formatearPesos(neto)}</dd>
        </div>
        <div>
          <dt>IVA ({porcentaje}%)</dt>
          <dd>{formatearPesos(iva)}</dd>
        </div>
        <div>
          <dt>Total</dt>
          <dd>{formatearPesos(total)}</dd>
        </div>
      </dl>

      {tipoComprobante === 'facturaC' && (
        <p>Una Factura C no lleva IVA: el total es igual al neto.</p>
      )}
    </section>
  )
}
