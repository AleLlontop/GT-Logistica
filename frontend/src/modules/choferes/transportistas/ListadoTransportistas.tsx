import { Aviso } from '../../../compartido/ui/Aviso'
import { EstadoVacio } from '../../../compartido/ui/EstadoVacio'
import { Listado, TablaDesplazable } from '../../../compartido/ui/Listado'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { useCallback, useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { ConfirmacionBaja } from '../componentes/ConfirmacionBaja'
import { FiltrosTransportistas } from '../componentes/FiltrosTransportistas'
import { FILTROS_TRANSPORTISTAS_VACIOS } from '../servicios/formato'
import {
  darDeBajaTransportista,
  listarTransportistas,
  type Transportista,
} from './servicioTransportistas'

// Dos mensajes distintos a propósito (FR-023): "todavía no cargaste ninguno" y "tu búsqueda no
// encontró nada" son situaciones distintas y llevan a acciones distintas. Los textos son los que
// fija `contracts/README.md`.
const MENSAJE_PADRON_VACIO =
  'Todavía no hay transportistas cargados. Registrá el primero para poder asignarle choferes.'
const MENSAJE_SIN_COINCIDENCIAS = 'No hay transportistas que coincidan con la búsqueda.'

/**
 * Padrón de transportistas (User Story 1).
 *
 * El padrón arranca vacío en toda instalación nueva —G&T Logística S.A. se carga desde acá como
 * cualquier otro (FR-004)—, así que la lista vacía es el estado inicial esperado y no un error.
 */
export function ListadoTransportistas() {
  const navegar = useNavigate()

  const [filtros, setFiltros] = useState(FILTROS_TRANSPORTISTAS_VACIOS)
  const [transportistas, setTransportistas] = useState<Transportista[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [aviso, setAviso] = useState<string | null>(null)
  const [aBajar, setABajar] = useState<Transportista | null>(null)

  const traer = useCallback(() => {
    listarTransportistas(filtros.texto, filtros.soloActivos)
      .then((lista) => {
        setTransportistas(lista)
        setError(null)
      })
      .catch(() =>
        setError('No pudimos traer el padrón de transportistas. Volvé a intentar en unos minutos.'),
      )
  }, [filtros])

  useEffect(() => {
    traer()
  }, [traer])

  async function confirmarBaja() {
    if (aBajar === null) {
      return
    }

    try {
      await darDeBajaTransportista(aBajar.id)
      setError(null)
      setAviso(`${aBajar.nombre} quedó inactivo.`)
      traer()
    } catch (fallo) {
      // Acá cae el rechazo de FR-010: tiene choferes activos. El mensaje dice cuántos.
      setError(
        fallo instanceof ErrorHttp
          ? fallo.detalle.mensaje
          : 'Ocurrió un problema inesperado. Volvé a intentar en unos minutos.',
      )
    } finally {
      setABajar(null)
    }
  }

  function formatearCuit(cuit: string) {
    if (cuit.length !== 11) return cuit
    return `${cuit.slice(0, 2)}-${cuit.slice(2, 10)}-${cuit.slice(10)}`
  }

  function formatearTipo(tipo: Transportista['tipo']) {
    return tipo === 'fisica' ? 'Física' : 'Jurídica'
  }

  // Filtrar por "sólo activos" también es buscar: si esconde a todos, no es que el padrón esté
  // vacío.
  const filtrando = filtros.texto.trim() !== '' || filtros.soloActivos

  return (
    <section>
      <EncabezadoDePantalla
        titulo="Transportistas"
        accionPrincipal={
          <>
            <Link to="/transportistas/nuevo">Nuevo transportista</Link>
          </>
        }
      />
      <FiltrosTransportistas valor={filtros} onCambio={setFiltros} />

      {error !== null && (
        <Aviso tono="error" rol="alert" className="mb-4">
          {error}
        </Aviso>
      )}
      {aviso !== null && <p role="status">{aviso}</p>}

      {transportistas === null && error === null && (
        <EstadoVacio caso="cargando" className="border-0 shadow-none">
          Cargando transportistas…
        </EstadoVacio>
      )}

      {transportistas !== null && transportistas.length === 0 && (
        <EstadoVacio
          caso={filtrando ? 'sinCoincidencias' : 'vacio'}
          className="border-0 shadow-none"
        >
          {filtrando ? MENSAJE_SIN_COINCIDENCIAS : MENSAJE_PADRON_VACIO}
        </EstadoVacio>
      )}

      {transportistas !== null && transportistas.length > 0 && (
        <Listado>
          <TablaDesplazable>
            <table>
          <caption>Padrón de transportistas</caption>
          <thead>
            <tr>
              <th scope="col">Nombre</th>
              <th scope="col">CUIT</th>
              <th scope="col">Tipo de persona</th>
              <th scope="col">Teléfono</th>
              <th scope="col">Email</th>
              <th scope="col">Estado</th>
              <th scope="col">Choferes activos</th>
              {/* Módulo 4, FR-008d: la baja mira también la flota, así que la pantalla muestra las
                  dos cantidades. Es lo que explica por qué algunos no se pueden dar de baja. */}
              <th scope="col">Vehículos activos</th>
              <th scope="col">Acciones</th>
            </tr>
          </thead>
          <tbody>
            {transportistas.map((transportista) => (
              <tr key={transportista.id}>
                <td>{transportista.nombre}</td>
                <td>{formatearCuit(transportista.cuit)}</td>
                <td>{formatearTipo(transportista.tipo)}</td>
                <td>{transportista.telefono}</td>
                <td>{transportista.email}</td>
                <td>{transportista.activo ? 'Activo' : 'Inactivo'}</td>
                <td>{transportista.choferesActivos}</td>
                <td>{transportista.vehiculosActivos}</td>
                <td>
                  <button
                    type="button"
                    onClick={() => navegar(`/transportistas/${transportista.id}/editar`)}
                  >
                    Editar
                  </button>
                  {transportista.activo && (
                    <button type="button" onClick={() => setABajar(transportista)}>
                      Dar de baja
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
          </TablaDesplazable>
        </Listado>
      )}

      {aBajar !== null && (
        <ConfirmacionBaja
          que={{ tipo: 'transportista', nombre: aBajar.nombre }}
          onConfirmar={confirmarBaja}
          onCancelar={() => setABajar(null)}
        />
      )}
    </section>
  )
}
