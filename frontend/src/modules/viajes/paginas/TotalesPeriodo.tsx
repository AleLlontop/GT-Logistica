import { useEffect, useState } from 'react'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { formatearPesos } from '../../../compartido/moneda'
import {
  consultarTotales,
  type TotalDelPeriodo,
  type TotalesDelPeriodo,
} from '../servicios/servicioViajes'

const MENSAJE_FALTA_RANGO = 'Elegí un rango de fechas para ver los totales.'
const MENSAJE_SIN_RESULTADOS = 'No hay viajes en el período elegido.'

/**
 * Totales por cliente y por transportista en un período (User Story 7).
 *
 * **El rango es obligatorio y la pantalla no calcula nada sin él.** No es una validación defensiva:
 * un total "de todo" no responde ninguna pregunta real —Administración le arma a Gerencia el resumen
 * de un mes— y sería el número más caro y menos útil que el sistema puede dar (FR-046a).
 *
 * **La fecha de corte es la fecha del viaje**, y los anulados no figuran en ninguna cantidad ni en
 * ningún importe (FR-046a, FR-047).
 */
export function TotalesPeriodo() {
  const [desde, setDesde] = useState('')
  const [hasta, setHasta] = useState('')
  const [totales, setTotales] = useState<TotalesDelPeriodo | null>(null)
  const [error, setError] = useState<string | null>(null)

  const rangoElegido = desde !== '' && hasta !== ''

  useEffect(() => {
    if (!rangoElegido) {
      // Sin rango no se pide nada: la pantalla no calcula "por las dudas".
      setTotales(null)
      return
    }

    let vigente = true

    consultarTotales(desde, hasta)
      .then((totales) => {
        if (!vigente) return
        setTotales(totales)
        setError(null)
      })
      .catch((fallo) => {
        if (!vigente) return
        setError(
          fallo instanceof ErrorHttp
            ? fallo.detalle.mensaje
            : 'No pudimos traer los totales. Volvé a intentar en unos minutos.',
        )
      })

    return () => {
      vigente = false
    }
  }, [desde, hasta, rangoElegido])

  const sinResultados =
    totales !== null && totales.porCliente.length === 0 && totales.porTransportista.length === 0

  return (
    <main>
      <h1>Totales por período</h1>

      <form onSubmit={(evento) => evento.preventDefault()}>
        <div className="campo">
          <label htmlFor="totales-desde">Desde</label>
          <input
            id="totales-desde"
            type="date"
            value={desde}
            onChange={(evento) => setDesde(evento.target.value)}
          />
        </div>

        <div className="campo">
          <label htmlFor="totales-hasta">Hasta</label>
          <input
            id="totales-hasta"
            type="date"
            value={hasta}
            onChange={(evento) => setHasta(evento.target.value)}
          />
        </div>
      </form>

      {error !== null && <p role="alert">{error}</p>}

      {!rangoElegido && <p role="status">{MENSAJE_FALTA_RANGO}</p>}

      {rangoElegido && sinResultados && <p role="status">{MENSAJE_SIN_RESULTADOS}</p>}

      {totales !== null && totales.porCliente.length > 0 && (
        <Cuadro
          titulo="Por cliente"
          encabezado="Cliente"
          filas={totales.porCliente}
        />
      )}

      {totales !== null && totales.porTransportista.length > 0 && (
        <Cuadro
          titulo="Por transportista"
          encabezado="Transportista"
          filas={totales.porTransportista}
        />
      )}
    </main>
  )
}

function Cuadro({
  titulo,
  encabezado,
  filas,
}: {
  titulo: string
  encabezado: string
  filas: TotalDelPeriodo[]
}) {
  return (
    <section>
      <h2>{titulo}</h2>

      <table>
        <caption>{titulo}</caption>
        <thead>
          <tr>
            <th scope="col">{encabezado}</th>
            <th scope="col">Viajes</th>
            <th scope="col">Importe</th>
          </tr>
        </thead>
        <tbody>
          {filas.map((fila) => (
            <tr key={fila.id}>
              <td>{fila.nombre}</td>
              <td>{fila.cantidadViajes}</td>
              <td>{formatearPesos(fila.importeTotal)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  )
}
