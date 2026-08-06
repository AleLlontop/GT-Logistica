import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { claseDeEstado, formatearFecha, TEXTO_ESTADO_DOCUMENTO, textoDelPlazo } from '../servicios/estados'
import { listarVencimientos, type AlertaVencimiento } from '../servicios/servicioChoferes'

const MENSAJE_SIN_VENCIMIENTOS = 'No hay documentación próxima a vencer ni vencida.'

/**
 * Panel de vencimientos (User Story 5).
 *
 * Muestra, al entrar al módulo, qué choferes necesitan renovar algo. **Nadie ejecuta nada**: el
 * estado se calcula al consultar, así que un documento entra al panel solo el día que le toca
 * (FR-019).
 *
 * Sólo entran choferes activos y documentos vigentes de su tipo: un chofer dado de baja no alerta
 * aunque tenga todo vencido, y una licencia vieja ya renovada tampoco (FR-021, FR-020a).
 */
export function PanelVencimientos() {
  const [alertas, setAlertas] = useState<AlertaVencimiento[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    listarVencimientos()
      .then((lista) => {
        setAlertas(lista)
        setError(null)
      })
      .catch(() =>
        setError('No pudimos traer los vencimientos. Volvé a intentar en unos minutos.'),
      )
  }, [])

  return (
    <main>
      <h1>Vencimientos</h1>

      <Link to="/choferes">Volver al listado de choferes</Link>

      {error !== null && <p role="alert">{error}</p>}

      {alertas === null && error === null && <p role="status">Cargando vencimientos…</p>}

      {/* Una lista vacía es una buena noticia, y se dice: no se muestra una tabla vacía. */}
      {alertas !== null && alertas.length === 0 && <p role="status">{MENSAJE_SIN_VENCIMIENTOS}</p>}

      {alertas !== null && alertas.length > 0 && (
        <table>
          <caption>Documentación próxima a vencer o vencida</caption>
          <thead>
            <tr>
              <th scope="col">Chofer</th>
              <th scope="col">Transportista</th>
              <th scope="col">Documento</th>
              <th scope="col">Vencimiento</th>
              <th scope="col">Estado</th>
            </tr>
          </thead>
          <tbody>
            {/* Ordenadas por urgencia desde el servidor: primero lo vencido hace más tiempo. */}
            {alertas.map((alerta) => (
              <tr key={alerta.documento.id}>
                <td>
                  <Link to={`/choferes/${alerta.choferId}`}>
                    {alerta.apellido}, {alerta.nombre}
                  </Link>
                </td>
                <td>{alerta.transportista.nombre}</td>
                <td>
                  {alerta.documento.tipo.nombre} N° {alerta.documento.numero}
                </td>
                <td>{formatearFecha(alerta.documento.fechaVencimiento)}</td>
                <td className={claseDeEstado(alerta.documento.estado)}>
                  {TEXTO_ESTADO_DOCUMENTO[alerta.documento.estado]} —{' '}
                  {textoDelPlazo(alerta.documento.diasHastaVencimiento)}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </main>
  )
}
