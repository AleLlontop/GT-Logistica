import { Estado } from '../../../compartido/ui/Estado'
import { Aviso } from '../../../compartido/ui/Aviso'
import { EstadoVacio } from '../../../compartido/ui/EstadoVacio'
import { Listado, TablaDesplazable } from '../../../compartido/ui/Listado'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { formatearFecha, TEXTO_ESTADO_DOCUMENTO, textoDelPlazo } from '../servicios/estados'
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
    <section>
      <EncabezadoDePantalla
        titulo="Vencimientos"
        accionPrincipal={
          <>
            <Link to="/choferes">Volver al listado de choferes</Link>
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

      {/* Una lista vacía es una buena noticia, y se dice: no se muestra una tabla vacía. */}
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
