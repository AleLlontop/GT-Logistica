import { Estado } from '../../../compartido/ui/Estado'
import { MenuDeFila } from '../../../compartido/ui/MenuDeFila'
import { clasesDeBoton } from '../../../compartido/ui/clases'
import { IconoNuevo } from '../../../compartido/ui/iconos'
import { Aviso } from '../../../compartido/ui/Aviso'
import { EstadoVacio } from '../../../compartido/ui/EstadoVacio'
import { Listado, TablaDesplazable } from '../../../compartido/ui/Listado'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
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
          <Link to="/transportistas/nuevo" className={clasesDeBoton('primario')}>
            Nuevo transportista
            <span
              aria-hidden="true"
              className="flex size-8 shrink-0 items-center justify-center rounded-pastilla bg-white/[0.14] transition-transform duration-200 ease-gt group-hover:translate-x-0.5"
            >
              <IconoNuevo className="size-3" />
            </span>
          </Link>
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
        <FiltrosTransportistas
          valor={filtros}
          onCambio={setFiltros}
          resumen={
            transportistas !== null && (
              <>
                <span>
                  {transportistas.length}{' '}
                  {transportistas.length === 1 ? 'transportista' : 'transportistas'}
                </span>
                <span>Ordenado por nombre</span>
              </>
            )
          }
        />

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
          <TablaDesplazable>
            <table>
              <caption>Padrón de transportistas</caption>
              <thead>
                <tr>
                  {/* `Nombre` + `CUIT` nombran un solo concepto: el transportista (FR-047). */}
                  <th scope="col">Transportista</th>
                  <th scope="col">Tipo de persona</th>
                  {/* `Teléfono` + `Email` nombran un solo concepto: el contacto (FR-047). */}
                  <th scope="col">Contacto</th>
                  <th scope="col">Estado</th>
                  <th scope="col">Choferes activos</th>
                  {/* Módulo 4, FR-008d: la baja mira también la flota, así que la pantalla muestra
                      las dos cantidades. Es lo que explica por qué algunos no se pueden dar de
                      baja. */}
                  <th scope="col">Vehículos activos</th>
                  <th scope="col" className="w-8" aria-hidden="true" />
                </tr>
              </thead>
              <tbody>
                {/* **Sin enlace de fila**: no tiene ficha de destino, y su único destino es la
                    edición, que va al `···`. */}
                {transportistas.map((transportista) => (
                  <tr key={transportista.id}>
                    <td>
                      <span className="flex flex-col gap-0.5">
                        <span className="font-semibold tracking-tight">
                          {transportista.nombre}
                        </span>
                        <span className="font-mono text-[11.5px] text-faint">
                          {formatearCuit(transportista.cuit)}
                        </span>
                      </span>
                    </td>
                    <td>{formatearTipo(transportista.tipo)}</td>
                    <td>
                      <span className="flex flex-col gap-0.5">
                        <span>{transportista.telefono}</span>
                        <span className="text-[11.5px] text-faint">{transportista.email}</span>
                      </span>
                    </td>
                    <td>
                      <Estado
                        valor={transportista.activo ? 'activo' : 'inactivo'}
                        texto={transportista.activo ? 'Activo' : 'Inactivo'}
                        forma="punto"
                      />
                    </td>
                    <td>{transportista.choferesActivos}</td>
                    <td>{transportista.vehiculosActivos}</td>
                    <td className="w-8">
                      <MenuDeFila
                        etiqueta={`Acciones de ${transportista.nombre}`}
                        items={[
                          {
                            etiqueta: 'Editar',
                            a: `/transportistas/${transportista.id}/editar`,
                          },
                          ...(transportista.activo
                            ? [
                                {
                                  etiqueta: 'Dar de baja',
                                  onSeleccionar: () => setABajar(transportista),
                                  destructivo: true,
                                },
                              ]
                            : []),
                        ]}
                      />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </TablaDesplazable>
        )}
      </Listado>

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
