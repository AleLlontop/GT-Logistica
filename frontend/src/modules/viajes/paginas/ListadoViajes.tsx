import { useCallback, useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { formatearFecha } from '../../../compartido/fechas'
import { formatearPesos } from '../../../compartido/moneda'
import { FiltrosViajes } from '../componentes/FiltrosViajes'
import { Paginacion } from '../componentes/Paginacion'
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
export function ListadoViajes({ puedeGestionar }: Props) {
  const navegar = useNavigate()

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
    <main>
      <h1>Viajes</h1>

      {puedeGestionar && <Link to="/viajes/nuevo">Nuevo viaje</Link>}

      <FiltrosViajes valor={filtros} onCambio={cambiarFiltros} />

      {/* El control nunca oculta filas en silencio: si no se eligió estado, dice qué está mostrando
          (FR-044, FR-049). */}
      <p role="status">
        {filtros.estado === ''
          ? 'Mostrando todos los viajes menos los anulados. Elegí "Anulado" para verlos.'
          : `Mostrando sólo: ${NOMBRES_DE_ESTADO[filtros.estado]}.`}
      </p>

      {error !== null && <p role="alert">{error}</p>}

      {resultado === null && error === null && <p role="status">Cargando viajes…</p>}

      {resultado !== null && resultado.items.length === 0 && (
        <p role="status">{filtrando ? MENSAJE_SIN_COINCIDENCIAS : MENSAJE_SIN_VIAJES}</p>
      )}

      {resultado !== null && resultado.items.length > 0 && (
        <table>
          <caption>Viajes registrados</caption>
          <thead>
            <tr>
              <th scope="col">Número</th>
              <th scope="col">Fecha</th>
              <th scope="col">Cliente</th>
              <th scope="col">Origen</th>
              <th scope="col">Destino</th>
              <th scope="col">Chofer</th>
              <th scope="col">Vehículo</th>
              <th scope="col">Transportista</th>
              <th scope="col">Estado</th>
              <th scope="col">Importe</th>
            </tr>
          </thead>
          <tbody>
            {resultado.items.map((viaje) => (
              <tr key={viaje.id}>
                <td>
                  <button type="button" onClick={() => navegar(`/viajes/${viaje.id}`)}>
                    {viaje.numero}
                  </button>
                </td>
                <td>
                  {formatearFecha(viaje.fecha)}
                  {viaje.esRetroactivo && <span> Carga retroactiva</span>}
                </td>
                <td>{nombreConEstado(viaje.cliente)}</td>
                <td>{viaje.origen}</td>
                <td>{viaje.destino}</td>
                <td>{nombreConEstado(viaje.chofer)}</td>
                <td>{nombreConEstado(viaje.vehiculo)}</td>
                <td>{nombreConEstado(viaje.transportista)}</td>
                <td>
                  {NOMBRES_DE_ESTADO[viaje.estado]}
                  {/* `Demorado` acompaña al estado; no lo reemplaza: el viaje sigue en curso
                      (FR-039). */}
                  {viaje.demorado && <span> Demorado</span>}
                </td>
                <td>{formatearPesos(viaje.importe)}</td>
              </tr>
            ))}
          </tbody>
          {/* Sin pie de tabla: los totales viven en su pantalla, y sumar la página daría un número
              que no es el del período (FR-046a). */}
        </table>
      )}

      {/* Al filtrar por anulado, cada fila muestra su motivo (FR-036, US6 esc. 5). */}
      {filtros.estado === 'anulado' && resultado !== null && resultado.items.length > 0 && (
        <section>
          <h2>Motivos de anulación</h2>
          <dl>
            {resultado.items.map((viaje) => (
              <div key={viaje.id}>
                <dt>Viaje {viaje.numero}</dt>
                <dd>{viaje.motivoAnulacion ?? '—'}</dd>
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
          entidad="viajes"
          onCambiarPagina={setPagina}
        />
      )}
    </main>
  )
}
