import { Estado } from '../../../compartido/ui/Estado'
import { Aviso } from '../../../compartido/ui/Aviso'
import { EstadoVacio } from '../../../compartido/ui/EstadoVacio'
import { Listado, TablaDesplazable } from '../../../compartido/ui/Listado'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import {
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
    <section>
      <EncabezadoDePantalla
        titulo="Vencimientos de la flota"
        accionPrincipal={
          <>
            <Link to="/flota">Volver al listado de flota</Link>
          </>
        }
      />
      {error !== null && (
        <Aviso tono="error" rol="alert" className="mb-4">
          {error}
        </Aviso>
      )}

      {alertas === null && error === null && (
        <EstadoVacio caso="cargando" className="border-0 shadow-none">
          Cargando vencimientos…
        </EstadoVacio>
      )}

      {/* Una lista vacía es una buena noticia, y se dice: no se muestra una tabla vacía (FR-036). */}
      {alertas !== null && alertas.length === 0 && <EstadoVacio caso="vacio" className="border-0 shadow-none">
          {MENSAJE_SIN_VENCIMIENTOS}
        </EstadoVacio>}

      {alertas !== null && alertas.length > 0 && (
        <Listado>
          <TablaDesplazable>
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
                <td>
                  <Estado valor={alerta.documento.estado} texto={TEXTO_ESTADO_DOCUMENTO[alerta.documento.estado]} /> —{' '}
                  {textoDelPlazo(alerta.documento.diasHastaVencimiento)}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
          </TablaDesplazable>
        </Listado>
      )}
    </section>
  )
}
