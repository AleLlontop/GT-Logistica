import { useCallback, useEffect, useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { ConfirmacionBajaCliente } from '../componentes/ConfirmacionBajaCliente'
import { Paginacion } from '../componentes/Paginacion'
import {
  darDeAltaCliente,
  darDeBajaCliente,
  FILTROS_CLIENTES_INICIALES,
  listarClientes,
  type Cliente,
  type PaginaDe,
} from './servicioClientes'

// Dos mensajes distintos a propósito: "todavía no cargaste ninguno" y "tu búsqueda no encontró nada"
// son situaciones distintas y llevan a acciones distintas. Los textos son los de contracts/README.md.
const MENSAJE_PADRON_VACIO =
  'Todavía no hay clientes cargados. Registrá el primero para poder empezar a cargar viajes.'
const MENSAJE_SIN_COINCIDENCIAS = 'Ningún cliente coincide con los filtros aplicados.'

const MENSAJE_ERROR_GENERICO = 'Ocurrió un problema inesperado. Volvé a intentar en unos minutos.'

interface Props {
  /**
   * `viajes.gestionar`. Quien sólo consulta ve el padrón y no ve ningún botón de escritura (FR-052).
   * Ocultarlos es una cortesía: la restricción la aplica el servidor (SC-012).
   */
  puedeGestionar: boolean
}

/**
 * Padrón de clientes (User Story 1).
 *
 * El padrón arranca vacío en toda instalación nueva, así que la lista vacía es el estado inicial
 * esperado y no un error (FR-009, US1 esc. 1).
 *
 * **Un cliente inactivo se muestra atenuado y además con la palabra `Inactivo`** al lado de su razón
 * social: ningún estado se comunica sólo por color (FR-049).
 */
export function ListadoClientes({ puedeGestionar }: Props) {
  const navegar = useNavigate()
  const ubicacion = useLocation()

  const [filtros, setFiltros] = useState(FILTROS_CLIENTES_INICIALES)
  const [pagina, setPagina] = useState(1)
  const [resultado, setResultado] = useState<PaginaDe<Cliente> | null>(null)
  const [error, setError] = useState<string | null>(null)

  // La confirmación del alta o de la edición llega desde el formulario y se anuncia acá: el guardado
  // ocurrió en otra pantalla, y sin esto quien opera vuelve al listado sin saber si salió bien.
  const [aviso, setAviso] = useState<string | null>(
    (ubicacion.state as { aviso?: string } | null)?.aviso ?? null,
  )
  const [aBajar, setABajar] = useState<Cliente | null>(null)

  const traer = useCallback(() => {
    listarClientes(filtros, pagina)
      .then((pagina) => {
        setResultado(pagina)
        setError(null)
      })
      .catch(() => setError('No pudimos traer el padrón de clientes. Volvé a intentar en unos minutos.'))
  }, [filtros, pagina])

  useEffect(() => {
    traer()
  }, [traer])

  async function confirmarBaja() {
    if (aBajar === null) {
      return
    }

    try {
      await darDeBajaCliente(aBajar.id)
      setError(null)
      setAviso(`${aBajar.razonSocial} quedó dado de baja. Deja de ofrecerse al registrar viajes.`)
      traer()
    } catch (fallo) {
      // Acá cae el rechazo de FR-006: tiene viajes pendientes o en curso. El mensaje dice cuántos.
      setError(fallo instanceof ErrorHttp ? fallo.detalle.mensaje : MENSAJE_ERROR_GENERICO)
    } finally {
      setABajar(null)
    }
  }

  /** Sin confirmación aparte: no destruye nada y se deshace con la baja (FR-007). */
  async function darDeAlta(cliente: Cliente) {
    try {
      await darDeAltaCliente(cliente.id)
      setError(null)
      setAviso(`${cliente.razonSocial} volvió al padrón. Se ofrece de nuevo al registrar viajes.`)
      traer()
    } catch (fallo) {
      setError(fallo instanceof ErrorHttp ? fallo.detalle.mensaje : MENSAJE_ERROR_GENERICO)
    }
  }

  function formatearCuit(cuit: string) {
    return cuit.length === 11 ? `${cuit.slice(0, 2)}-${cuit.slice(2, 10)}-${cuit.slice(10)}` : cuit
  }

  // Filtrar también es buscar: si el filtro esconde a todos, no es que el padrón esté vacío.
  const filtrando = filtros.busqueda.trim() !== '' || filtros.soloActivos

  return (
    <main>
      <h1>Clientes</h1>

      {puedeGestionar && <Link to="/clientes/nuevo">Nuevo cliente</Link>}

      <form
        onSubmit={(evento) => {
          evento.preventDefault()
          setPagina(1)
        }}
      >
        <label htmlFor="busqueda-clientes">Buscar por razón social</label>
        <input
          id="busqueda-clientes"
          type="search"
          value={filtros.busqueda}
          onChange={(evento) => {
            setFiltros({ ...filtros, busqueda: evento.target.value })
            setPagina(1)
          }}
        />

        <label htmlFor="solo-activos">Mostrar sólo los activos</label>
        <input
          id="solo-activos"
          type="checkbox"
          checked={filtros.soloActivos}
          onChange={(evento) => {
            setFiltros({ ...filtros, soloActivos: evento.target.checked })
            setPagina(1)
          }}
        />
      </form>

      {error !== null && <p role="alert">{error}</p>}
      {aviso !== null && <p role="status">{aviso}</p>}

      {resultado === null && error === null && <p role="status">Cargando clientes…</p>}

      {resultado !== null && resultado.items.length === 0 && (
        <p role="status">{filtrando ? MENSAJE_SIN_COINCIDENCIAS : MENSAJE_PADRON_VACIO}</p>
      )}

      {resultado !== null && resultado.items.length > 0 && (
        <table>
          <caption>Padrón de clientes</caption>
          <thead>
            <tr>
              <th scope="col">Razón social</th>
              <th scope="col">CUIT</th>
              <th scope="col">Teléfono</th>
              <th scope="col">Email</th>
              <th scope="col">Estado</th>
              {puedeGestionar && <th scope="col">Acciones</th>}
            </tr>
          </thead>
          <tbody>
            {resultado.items.map((cliente) => (
              <tr key={cliente.id} className={cliente.activo ? undefined : 'atenuado'}>
                <td>
                  {cliente.razonSocial}
                  {/* Atenuar es una señal visual; la palabra es la que lo explica (FR-049). */}
                  {!cliente.activo && ' (inactivo)'}
                </td>
                <td>{formatearCuit(cliente.cuit)}</td>
                <td>{cliente.telefono}</td>
                <td>{cliente.email}</td>
                <td>{cliente.activo ? 'Activo' : 'Inactivo'}</td>
                {puedeGestionar && (
                  <td>
                    <button type="button" onClick={() => navegar(`/clientes/${cliente.id}`)}>
                      Editar
                    </button>

                    {cliente.activo ? (
                      <button type="button" onClick={() => setABajar(cliente)}>
                        Dar de baja
                      </button>
                    ) : (
                      <button type="button" onClick={() => darDeAlta(cliente)}>
                        Dar de alta
                      </button>
                    )}
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {resultado !== null && (
        <Paginacion
          pagina={resultado.pagina}
          total={resultado.total}
          tamanioPagina={resultado.tamanioPagina}
          entidad="clientes"
          onCambiarPagina={setPagina}
        />
      )}

      {aBajar !== null && (
        <ConfirmacionBajaCliente
          razonSocial={aBajar.razonSocial}
          onConfirmar={confirmarBaja}
          onCancelar={() => setABajar(null)}
        />
      )}
    </main>
  )
}
