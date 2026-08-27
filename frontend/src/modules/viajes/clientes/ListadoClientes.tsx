import { Aviso } from '../../../compartido/ui/Aviso'
import { EnlaceDeFila } from '../../../compartido/ui/EnlaceDeFila'
import { Estado } from '../../../compartido/ui/Estado'
import { EstadoVacio } from '../../../compartido/ui/EstadoVacio'
import { EncabezadoDeChevron, FilaNavegable } from '../../../compartido/ui/FilaNavegable'
import { Filtros, FranjaDeBusqueda, FranjaDeFiltros } from '../../../compartido/ui/Filtros'
import { Listado, TablaDesplazable } from '../../../compartido/ui/Listado'
import { MenuDeFila } from '../../../compartido/ui/MenuDeFila'
import {
  clasesDeBoton,
  clasesDeEtiquetaDeFiltro,
  clasesDeFiltro,
} from '../../../compartido/ui/clases'
import { IconoNuevo } from '../../../compartido/ui/iconos'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { useCallback, useEffect, useState } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { ConfirmacionBajaCliente } from '../componentes/ConfirmacionBajaCliente'
import { Paginacion } from '../../../compartido/ui/Paginacion'
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
    <section>
      <EncabezadoDePantalla
        titulo="Clientes"
        accionPrincipal={
          puedeGestionar ? (
            <Link to="/clientes/nuevo" className={clasesDeBoton('primario')}>
              Nuevo cliente
              <span
                aria-hidden="true"
                className="flex size-8 shrink-0 items-center justify-center rounded-pastilla bg-white/[0.14] transition-transform duration-200 ease-gt group-hover:translate-x-0.5"
              >
                <IconoNuevo className="size-3" />
              </span>
            </Link>
          ) : undefined
        }
      />

      {error !== null && (
        <Aviso tono="error" rol="alert" className="mb-[18px]">
          {error}
        </Aviso>
      )}
      {aviso !== null && (
        <p role="status" className="mb-[18px] text-[13px] text-ink-soft">
          {aviso}
        </p>
      )}

      <Listado>
        {/*
          Los filtros estaban sueltos en la pantalla, con el mismo blob de selectores por
          descendiente que [007] prohíbe. Pasan a la primitiva: el buscador **primero y a todo el
          ancho** y la casilla debajo (FR-032, FR-033). Los dos textos se conservan tal cual: los
          consulta la suite (FR-066).
        */}
        <Filtros
          resumen={
            resultado !== null && (
              <>
                <span>
                  {resultado.total} {resultado.total === 1 ? 'cliente' : 'clientes'}
                </span>
                <span>Ordenado por razón social</span>
              </>
            )
          }
        >
          <FranjaDeBusqueda>
            <div className="flex flex-col gap-1.5">
              <label htmlFor="busqueda-clientes" className={clasesDeEtiquetaDeFiltro}>
                Buscar por razón social
              </label>
              <input
                id="busqueda-clientes"
                type="search"
                placeholder="Distribuidora del Litoral"
                value={filtros.busqueda}
                onChange={(evento) => {
                  setFiltros({ ...filtros, busqueda: evento.target.value })
                  setPagina(1)
                }}
                className={clasesDeFiltro(filtros.busqueda.trim() !== '')}
              />
            </div>
          </FranjaDeBusqueda>

          <FranjaDeFiltros>
            <label
              htmlFor="solo-activos"
              className="flex items-center gap-2 text-[12.5px] font-medium text-ink-soft"
            >
              <input
                id="solo-activos"
                type="checkbox"
                checked={filtros.soloActivos}
                onChange={(evento) => {
                  setFiltros({ ...filtros, soloActivos: evento.target.checked })
                  setPagina(1)
                }}
                className="size-4"
              />
              Mostrar sólo los activos
            </label>
          </FranjaDeFiltros>
        </Filtros>

        {resultado === null && error === null && (
          <EstadoVacio caso="cargando" className="border-0 shadow-none">
            Cargando clientes…
          </EstadoVacio>
        )}

        {resultado !== null && resultado.items.length === 0 && (
          <EstadoVacio
            caso={filtrando ? 'sinCoincidencias' : 'vacio'}
            className="border-0 shadow-none"
          >
            {filtrando ? MENSAJE_SIN_COINCIDENCIAS : MENSAJE_PADRON_VACIO}
          </EstadoVacio>
        )}

        {resultado !== null && resultado.items.length > 0 && (
          <TablaDesplazable>
            <table>
              <caption>Padrón de clientes</caption>
              <thead>
                <tr>
                  {/* `Razón social` + `CUIT` nombran un solo concepto: el cliente (FR-047). */}
                  <th scope="col">Cliente</th>
                  {/* `Teléfono` + `Email` nombran un solo concepto: el contacto (FR-047). */}
                  <th scope="col">Contacto</th>
                  <th scope="col">Estado</th>
                  <EncabezadoDeChevron />
                </tr>
              </thead>
              <tbody>
                {resultado.items.map((cliente) => (
                  <FilaNavegable
                    key={cliente.id}
                    a={`/clientes/${cliente.id}`}
                    className={cliente.activo ? undefined : 'atenuado'}
                  >
                    <td>
                      <EnlaceDeFila
                        a={`/clientes/${cliente.id}`}
                        identificador={formatearCuit(cliente.cuit)}
                      >
                        {cliente.razonSocial}
                        {/* Atenuar es una señal visual; la palabra es la que lo explica (FR-060). */}
                        {!cliente.activo && ' (inactivo)'}
                      </EnlaceDeFila>
                    </td>
                    <td>
                      <span className="flex flex-col gap-0.5">
                        <span>{cliente.telefono}</span>
                        <span className="text-[11.5px] text-faint">{cliente.email}</span>
                      </span>
                    </td>
                    {/* Alta y baja: van como punto (FR-055). */}
                    <td>
                      <Estado
                        valor={cliente.activo ? 'activo' : 'inactivo'}
                        texto={cliente.activo ? 'Activo' : 'Inactivo'}
                        forma="punto"
                      />
                    </td>
                    <td className="w-8">
                      {/*
                        Sin permiso de gestión **el `···` no se dibuja**: `items` vacío no dibuja
                        nada (FR-045). Ocultarlo es cortesía; la restricción sigue siendo el `403`.
                      */}
                      <MenuDeFila
                        etiqueta={`Acciones de ${cliente.razonSocial}`}
                        items={
                          puedeGestionar
                            ? [
                                { etiqueta: 'Editar', a: `/clientes/${cliente.id}` },
                                cliente.activo
                                  ? {
                                      etiqueta: 'Dar de baja',
                                      onSeleccionar: () => setABajar(cliente),
                                      destructivo: true,
                                    }
                                  : {
                                      etiqueta: 'Dar de alta',
                                      onSeleccionar: () => darDeAlta(cliente),
                                    },
                              ]
                            : []
                        }
                      />
                    </td>
                  </FilaNavegable>
                ))}
              </tbody>
            </table>
          </TablaDesplazable>
        )}
      </Listado>

      {resultado !== null && (
        <Paginacion
          pagina={resultado.pagina}
          total={resultado.total}
          tamanioPagina={resultado.tamanioPagina}
          nombrePlural="clientes"
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
    </section>
  )
}
