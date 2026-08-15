import { useState, type FormEvent } from 'react'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { formatearFecha } from '../../../compartido/fechas'
import { formatearPesos } from '../../../compartido/moneda'
import { consultarTotalesFacturados, type TotalPorCliente } from '../servicios/servicioFacturas'

export const MENSAJE_SIN_RANGO = 'Elegí un rango de fechas para ver los totales.'

export const NOTA_DE_LOS_TOTALES =
  'Las facturas anuladas no suman en ninguna columna. La fecha de corte es la fecha de facturación.'

/**
 * Totales facturados por cliente entre dos fechas (User Story 7, FR-061, FR-062).
 *
 * **El rango es obligatorio y sin elegirlo no se calcula ni se muestra nada**, y eso es el requisito: un
 * cuadro vacío se lee como "no hay facturas", que es una respuesta distinta de "todavía no me dijiste qué
 * período mirar" (FR-061).
 *
 * **La nota de que las anuladas no suman va debajo del cuadro y es permanente**: sin ella, quien compara
 * estos números contra una planilla no tiene forma de saber por qué no coinciden.
 */
export function TotalesFacturados() {
  const [desde, setDesde] = useState('')
  const [hasta, setHasta] = useState('')

  const [totales, setTotales] = useState<TotalPorCliente[] | null>(null)
  const [rangoConsultado, setRangoConsultado] = useState<{ desde: string; hasta: string } | null>(
    null,
  )
  const [consultando, setConsultando] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function consultar(evento: FormEvent) {
    evento.preventDefault()

    setConsultando(true)
    setError(null)

    try {
      setTotales(await consultarTotalesFacturados(desde, hasta))
      setRangoConsultado({ desde, hasta })
    } catch (fallo) {
      setTotales(null)
      setError(
        fallo instanceof ErrorHttp
          ? fallo.detalle.mensaje
          : 'No pudimos traer los totales. Volvé a intentar en unos minutos.',
      )
    } finally {
      setConsultando(false)
    }
  }

  const rangoCompleto = desde !== '' && hasta !== ''

  return (
    <main>
      <h1>Totales facturados</h1>

      <form onSubmit={consultar} noValidate>
        <div className="campo">
          <label htmlFor="totales-desde">Desde</label>
          <input
            id="totales-desde"
            type="date"
            required
            value={desde}
            onChange={(evento) => setDesde(evento.target.value)}
          />
        </div>

        <div className="campo">
          <label htmlFor="totales-hasta">Hasta</label>
          <input
            id="totales-hasta"
            type="date"
            required
            value={hasta}
            onChange={(evento) => setHasta(evento.target.value)}
          />
        </div>

        <button type="submit" disabled={consultando || !rangoCompleto}>
          Ver totales
        </button>
      </form>

      {error !== null && <p role="alert">{error}</p>}

      {/* Sin rango elegido no se calcula ni se muestra nada, y la pantalla lo dice (FR-061). */}
      {totales === null && error === null && <p role="status">{MENSAJE_SIN_RANGO}</p>}

      {totales !== null && totales.length === 0 && rangoConsultado !== null && (
        <p role="status">
          No hay facturas emitidas entre el {formatearFecha(rangoConsultado.desde)} y el{' '}
          {formatearFecha(rangoConsultado.hasta)}.
        </p>
      )}

      {totales !== null && totales.length > 0 && (
        <>
          <table>
            <caption>Totales facturados por cliente</caption>
            <thead>
              <tr>
                <th scope="col">Cliente</th>
                <th scope="col">Cantidad de facturas</th>
                <th scope="col">Facturado</th>
                <th scope="col">Cobrado</th>
                <th scope="col">Pendiente de cobro</th>
              </tr>
            </thead>
            <tbody>
              {totales.map((fila) => (
                <tr key={fila.clienteId}>
                  <td>{fila.razonSocial}</td>
                  <td>{fila.cantidad}</td>
                  <td>{formatearPesos(fila.facturado)}</td>
                  <td>{formatearPesos(fila.cobrado)}</td>
                  <td>{formatearPesos(fila.pendiente)}</td>
                </tr>
              ))}
            </tbody>
          </table>

          <p role="note">{NOTA_DE_LOS_TOTALES}</p>
        </>
      )}
    </main>
  )
}
