import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { formatearFecha } from '../../../compartido/fechas'
import { formatearPesos } from '../../../compartido/moneda'
import { situacion } from '../servicios/api'
import { consultarVencimientos, type FilaDeVencimiento } from '../servicios/servicioFacturas'

export const MENSAJE_PANEL_VACIO =
  'No hay facturas vencidas ni por vencer en los próximos 7 días.'

/**
 * Panel de vencimientos (User Story 5, FR-063).
 *
 * Las facturas `vencida` y las que vencen dentro de los próximos **7 días corridos**. Las `pagada` y
 * `anulada` no figuran: la exclusión vive en la consulta del servidor, no acá.
 *
 * **La situación va con la palabra, no sólo con un color** (FR-065): `Vencida hace 3 días`,
 * `Vence en 5 días`, `Vence hoy`. Es la diferencia entre un panel que se puede leer y uno que hay que
 * interpretar.
 *
 * **Un panel vacío es una respuesta legítima** y se dice con esas palabras, en vez de mostrar una tabla sin
 * filas que se lee como un error de carga.
 */
export function PanelVencimientos() {
  const [filas, setFilas] = useState<FilaDeVencimiento[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let vigente = true

    consultarVencimientos()
      .then((traidas) => {
        if (vigente) setFilas(traidas)
      })
      .catch(() => {
        if (vigente) {
          setError('No pudimos traer los vencimientos. Volvé a intentar en unos minutos.')
        }
      })

    return () => {
      vigente = false
    }
  }, [])

  return (
    <main>
      <h1>Vencimientos</h1>

      {error !== null && <p role="alert">{error}</p>}

      {filas === null && error === null && <p role="status">Cargando vencimientos…</p>}

      {filas !== null && filas.length === 0 && <p role="status">{MENSAJE_PANEL_VACIO}</p>}

      {filas !== null && filas.length > 0 && (
        <table>
          <caption>Facturas vencidas y por vencer en los próximos 7 días</caption>
          <thead>
            <tr>
              <th scope="col">Cliente</th>
              <th scope="col">Número</th>
              <th scope="col">Importe</th>
              <th scope="col">Vencimiento</th>
              <th scope="col">Situación</th>
            </tr>
          </thead>
          <tbody>
            {filas.map((fila) => (
              <tr key={fila.id}>
                <td>{fila.cliente}</td>
                <td>
                  <Link to={`/facturas/${fila.id}`}>{fila.numeroComprobante}</Link>
                </td>
                <td>{formatearPesos(fila.total)}</td>
                <td>{formatearFecha(fila.vencimientoPago)}</td>
                {/* La palabra, no el color (FR-065). */}
                <td>{situacion(fila.dias)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </main>
  )
}
