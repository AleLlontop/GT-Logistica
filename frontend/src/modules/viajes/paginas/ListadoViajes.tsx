import { Aviso } from '../../../compartido/ui/Aviso'
import { Estado } from '../../../compartido/ui/Estado'
import { EstadoVacio } from '../../../compartido/ui/EstadoVacio'
import { EncabezadoDeChevron, FilaNavegable } from '../../../compartido/ui/FilaNavegable'
import { Listado, TablaDesplazable } from '../../../compartido/ui/Listado'
import { TokenDeIdentificador } from '../../../compartido/ui/TokenDeIdentificador'
import { clasesDeBoton } from '../../../compartido/ui/clases'
import { IconoNuevo } from '../../../compartido/ui/iconos'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { formatearFecha } from '../../../compartido/fechas'
import { formatearPesos } from '../../../compartido/moneda'
import { GenerarReporte } from '../../reportes/componentes/GenerarReporte'
import { FiltrosViajes } from '../componentes/FiltrosViajes'
import { Paginacion } from '../../../compartido/ui/Paginacion'
import type { PaginaDe } from '../clientes/servicioClientes'
import {
  FILTROS_VIAJES_INICIALES,
  listarViajes,
  nombreConEstado,
  NOMBRES_DE_ESTADO,
  type ViajeListado,
} from '../servicios/servicioViajes'

// Dos mensajes distintos a propósito: "todavía no cargaste ninguno" y "tu búsqueda no encontró nada"
// son situaciones distintas y llevan a acciones distintas (contracts/README.md).
const MENSAJE_SIN_VIAJES = 'Todavía no hay viajes registrados. Registrá el primero para empezar.'
const MENSAJE_SIN_COINCIDENCIAS = 'Ningún viaje coincide con los filtros aplicados.'

interface Props {
  /** `viajes.gestionar`. Quien sólo consulta ve el listado sin el botón de alta (FR-052). */
  puedeGestionar: boolean
  /** `reportes.emitir`. Sin él, *Generar reporte* no se dibuja (Módulo 12, FR-013). */
  puedeEmitirReportes: boolean
}

/**
 * Listado de viajes (User Stories 2 y 5).
 *
 * **No lleva fila de total de importes**, y es deliberado: los totales viven en su propia pantalla, y
 * sumar la página en curso daría un número que no es el del período (FR-046a).
 *
 * **Las cuatro señales por fila llevan palabra y no sólo color** —el estado, `Demorado`,
 * `Carga retroactiva` y `(inactivo)` en cliente, chofer y vehículo— (FR-008, FR-016, FR-030, FR-039,
 * FR-049).
 *
 * Los importes se formatean con `formatearPesos` y las fechas con `formatearFecha`, nunca a mano
 * (Principio II, convención [003]).
 */
export function ListadoViajes({ puedeGestionar, puedeEmitirReportes }: Props) {

  const [filtros, setFiltros] = useState(FILTROS_VIAJES_INICIALES)
  const [pagina, setPagina] = useState(1)
  const [resultado, setResultado] = useState<PaginaDe<ViajeListado> | null>(null)
  const [error, setError] = useState<string | null>(null)

  const traer = useCallback(() => {
    listarViajes(filtros, pagina)
      .then((pagina) => {
        setResultado(pagina)
        setError(null)
      })
      .catch(() => setError('No pudimos traer los viajes. Volvé a intentar en unos minutos.'))
  }, [filtros, pagina])

  useEffect(() => {
    traer()
  }, [traer])

  // Cualquier cambio de filtro vuelve a la primera página: quedarse en la 3 de un resultado que
  // ahora tiene una sola página muestra una tabla vacía que parece un error.
  function cambiarFiltros(nuevos: typeof filtros) {
    setFiltros(nuevos)
    setPagina(1)
  }

  const filtrando =
    filtros.clienteId !== '' ||
    filtros.transportistaId !== '' ||
    filtros.estado !== '' ||
    filtros.desde !== '' ||
    filtros.hasta !== '' ||
    filtros.busqueda.trim() !== ''

  return (
    <section>
      <EncabezadoDePantalla
        titulo="Viajes"
        /*
          *Generar reporte* es **secundaria** (Módulo 12, FR-005): *Nuevo viaje* sigue siendo la única
          primaria, y para Gerencia —que no gestiona viajes— la pantalla sigue sin primaria, como
          antes de esta feature.

          Se le pasan **los filtros aplicados** y el `total` del listado, no los de la página visible:
          el reporte abarca todas las filas del filtro (FR-007).
        */
        accionSecundaria={
          <GenerarReporte
            reporte="viajes"
            puedeEmitir={puedeEmitirReportes}
            cantidadDeFilas={resultado?.total ?? 0}
            filtros={{
              clienteId: filtros.clienteId,
              transportistaId: filtros.transportistaId,
              estado: filtros.estado,
              desde: filtros.desde,
              hasta: filtros.hasta,
              busqueda: filtros.busqueda.trim(),
            }}
          />
        }
        accionPrincipal={
          puedeGestionar ? (
            <Link to="/viajes/nuevo" className={clasesDeBoton('primario')}>
              Nuevo viaje
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

      <Listado>
        <FiltrosViajes
          valor={filtros}
          onCambio={cambiarFiltros}
          declaracion={
            /* El control nunca oculta filas en silencio: si no se eligió estado, dice qué está
               mostrando (convención [003]). */
            <span role="status">
              {filtros.estado === ''
                ? 'Mostrando todos los viajes menos los anulados. Elegí "Anulado" para verlos.'
                : `Mostrando sólo: ${NOMBRES_DE_ESTADO[filtros.estado]}.`}
            </span>
          }
          resumen={
            resultado !== null && (
              <>
                <span>
                  {resultado.total} {resultado.total === 1 ? 'viaje' : 'viajes'}
                </span>
                <span>Ordenado por fecha, del más reciente al más viejo</span>
              </>
            )
          }
        />

        {resultado === null && error === null && (
          <EstadoVacio caso="cargando" className="border-0 shadow-none">
            Cargando viajes…
          </EstadoVacio>
        )}

        {resultado !== null && resultado.items.length === 0 && (
          <EstadoVacio
            caso={filtrando ? 'sinCoincidencias' : 'vacio'}
            className="border-0 shadow-none"
          >
            {filtrando ? MENSAJE_SIN_COINCIDENCIAS : MENSAJE_SIN_VIAJES}
          </EstadoVacio>
        )}

        {resultado !== null && resultado.items.length > 0 && (
          <TablaDesplazable>
            <table>
              <caption>Viajes registrados</caption>
              <thead>
                <tr>
                  <th scope="col">Número</th>
                  <th scope="col">Fecha</th>
                  <th scope="col">Cliente</th>
                  {/* `Origen` + `Destino` nombran un solo concepto: la ruta (FR-047). */}
                  <th scope="col">Ruta</th>
                  {/* `Chofer` + `Vehículo` nombran un solo concepto: la asignación (FR-047). */}
                  <th scope="col">Asignación</th>
                  <th scope="col">Transportista</th>
                  <th scope="col">Estado</th>
                  <th scope="col" className="text-right">
                    Importe
                  </th>
                  <EncabezadoDeChevron />
                </tr>
              </thead>
              <tbody>
                {resultado.items.map((viaje) => (
                  <FilaNavegable key={viaje.id} a={`/viajes/${viaje.id}`}>
                    {/* El número es el identificador del viaje: token en mono y enlace de la
                        fila (FR-040, FR-041). */}
                    <td>
                      <TokenDeIdentificador a={`/viajes/${viaje.id}`} numero={viaje.numero} />
                    </td>
                    <td>
                      {formatearFecha(viaje.fecha)}
                      {/* `Carga retroactiva` acompaña a la fecha y lleva su palabra (FR-055). */}
                      {viaje.esRetroactivo && (
                        <span className="atenuada block text-[11.5px]">Carga retroactiva</span>
                      )}
                    </td>
                    <td>{nombreConEstado(viaje.cliente)}</td>
                    <td className="whitespace-nowrap">
                      {viaje.origen} <span className="text-dim">→</span> {viaje.destino}
                    </td>
                    {/* La asignación: el chofer arriba y la patente en mono debajo. */}
                    <td>
                      <span className="flex flex-col gap-0.5">
                        <span>{nombreConEstado(viaje.chofer)}</span>
                        <span className="font-mono text-[11.5px] text-faint">
                          {nombreConEstado(viaje.vehiculo)}
                        </span>
                      </span>
                    </td>
                    <td>{nombreConEstado(viaje.transportista)}</td>
                    <td>
                      {/*
                        El dato accesorio del estado va **debajo** y **no repite su palabra**
                        (FR-057): la pastilla ya dice *Facturado*, así que el detalle es el
                        comprobante y su fecha, no "Facturado en …".
                      */}
                      <Estado
                        valor={viaje.estado}
                        texto={NOMBRES_DE_ESTADO[viaje.estado]}
                        forma="pastilla"
                        detalle={
                          viaje.factura ? (
                            <>
                              <span className="font-mono">{viaje.factura.numero}</span>
                              <span aria-hidden="true" className="text-dim">
                                {' · '}
                              </span>
                              {formatearFecha(viaje.factura.fecha)}
                            </>
                          ) : undefined
                        }
                      />
                      {/* `Demorado` acompaña al estado; no lo reemplaza: el viaje sigue en curso. */}
                      {viaje.demorado && (
                        <span className="mt-1 block text-[11.5px] font-semibold text-estado-pendiente">
                          Demorado
                        </span>
                      )}
                    </td>
                    <td className="text-right font-bold whitespace-nowrap">
                      {formatearPesos(viaje.importe)}
                    </td>
                  </FilaNavegable>
                ))}
              </tbody>
              {/* Sin pie de tabla: los totales viven en su pantalla, y sumar la página daría un
                  número que no es el del período (FR-046a del Módulo 5). */}
            </table>
          </TablaDesplazable>
        )}
      </Listado>

      {/* Al filtrar por anulado, cada fila muestra su motivo (Módulo 5, US6 esc. 5). */}
      {filtros.estado === 'anulado' && resultado !== null && resultado.items.length > 0 && (
        <section className="mt-[18px] rounded-card border border-line bg-surface p-[22px] shadow-card">
          <h2 className="m-0 mb-3 text-[14px] font-bold tracking-[-0.02em] text-ink">
            Motivos de anulación
          </h2>
          <dl className="m-0 grid grid-cols-[minmax(6rem,auto)_1fr] gap-x-6 gap-y-2 text-[13px]">
            {resultado.items.map((viaje) => (
              <div key={viaje.id} className="col-span-2 grid grid-cols-subgrid">
                <dt className="text-faint">Viaje {viaje.numero}</dt>
                {/* El vacío se escribe: dice qué falta, no un guión (FR-038). */}
                <dd className="m-0">
                  {viaje.motivoAnulacion ?? (
                    <span className="atenuada">Sin motivo registrado</span>
                  )}
                </dd>
              </div>
            ))}
          </dl>
        </section>
      )}

      {resultado !== null && (
        <Paginacion

          pagina={resultado.pagina}
          total={resultado.total}
          tamanioPagina={resultado.tamanioPagina}
          nombrePlural="viajes"
          onCambiarPagina={setPagina}
        />
      )}
    </section>
  )
}
