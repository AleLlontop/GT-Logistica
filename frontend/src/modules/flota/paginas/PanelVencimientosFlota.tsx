import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  claseDeEstado,
  formatearFecha,
  TEXTO_ESTADO_DOCUMENTO,
  textoDelPlazo,
} from '../servicios/estados'
import {
  listarVencimientosDeFlota,
  type AlertaVencimientoFlota,
} from '../servicios/servicioFlota'

const MENSAJE_SIN_VENCIMIENTOS = 'No hay vencimientos pendientes.'

/**
 * Panel de vencimientos de la flota (User Story 5).
 *
 * Muestra, al entrar al módulo, qué unidades necesitan renovar algo antes de quedar inhabilitadas
 * para circular. **Nadie ejecuta nada**: el estado se calcula al consultar, así que un documento
 * entra al panel solo el día que le toca (FR-022, SC-005).
 *
 * Sólo entran vehículos activos y documentos vigentes de su tipo: una unidad dada de baja no alerta
 * aunque tenga todo vencido, y un seguro viejo ya renovado tampoco (FR-035, FR-024).
 */
export function PanelVencimientosFlota() {
  const [alertas, setAlertas] = useState<AlertaVencimientoFlota[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    listarVencimientosDeFlota()
      .then((lista) => {
        setAlertas(lista)
        setError(null)
      })
      .catch(() => setError('No pudimos traer los vencimientos. Volvé a intentar en unos minutos.'))
  }, [])

  return (
    <main>
      <h1>Vencimientos de la flota</h1>

      <Link to="/flota">Volver al listado de flota</Link>

      {error !== null && <p role="alert">{error}</p>}

      {alertas === null && error === null && <p role="status">Cargando vencimientos…</p>}

      {/* Una lista vacía es una buena noticia, y se dice: no se muestra una tabla vacía (FR-036). */}
      {alertas !== null && alertas.length === 0 && <p role="status">{MENSAJE_SIN_VENCIMIENTOS}</p>}

      {alertas !== null && alertas.length > 0 && (
        <table>
          <caption>Documentación próxima a vencer o vencida</caption>
          <thead>
            <tr>
              <th scope="col">Patente</th>
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
                  {/* Cada fila lleva a la ficha de la unidad (US5 esc. 2). */}
                  <Link to={`/flota/${alerta.vehiculoId}`}>{alerta.patente}</Link>
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
