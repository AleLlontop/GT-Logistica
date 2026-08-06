import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { Paginacion } from '../componentes/Paginacion'
import { claseDeEstado, TEXTO_ESTADO_CHOFER } from '../servicios/estados'
import {
  FILTROS_CHOFERES_INICIALES,
  listarChoferes,
  type ChoferListado,
  type FiltrosChoferes,
  type PaginaDe,
} from '../servicios/servicioChoferes'
import { listarTransportistas, type Transportista } from '../transportistas/servicioTransportistas'

// Dos mensajes distintos a propósito (FR-023): "todavía no hay ninguno" y "tus filtros no
// encontraron nada" son situaciones distintas y llevan a acciones distintas.
const MENSAJE_SIN_CHOFERES = 'Todavía no hay choferes registrados.'
const MENSAJE_SIN_COINCIDENCIAS = 'No hay choferes que coincidan con los filtros aplicados.'

const ESTADOS_DE_DOCUMENTACION = [
  { valor: 'enRegla', etiqueta: 'En regla' },
  { valor: 'proximaAvencer', etiqueta: 'Próxima a vencer' },
  { valor: 'vencida', etiqueta: 'Vencida' },
  { valor: 'sinDocumentacion', etiqueta: 'Sin documentación' },
] as const

/**
 * Listado de choferes (User Story 4).
 *
 * Es la pantalla que responde, antes de asignar un viaje, si el chofer está en condiciones.
 *
 * El filtro de estado **arranca con `Activo` puesto, no vacío**, y a la vista: un listado que oculta
 * choferes sin decirlo se lee como un error de datos. Con el control visible, quien opera ve por qué
 * no está el chofer que dio de baja ayer, y lo encuentra cambiando el filtro (FR-022).
 */
export function ListadoChoferes() {
  const [filtros, setFiltros] = useState<FiltrosChoferes>(FILTROS_CHOFERES_INICIALES)
  const [pagina, setPagina] = useState(1)
  const [resultado, setResultado] = useState<PaginaDe<ChoferListado> | null>(null)
  const [transportistas, setTransportistas] = useState<Transportista[]>([])
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    listarTransportistas()
      .then(setTransportistas)
      .catch(() => setTransportistas([]))
  }, [])

  const traer = useCallback(() => {
    listarChoferes(filtros, pagina)
      .then((pagina) => {
        setResultado(pagina)
        setError(null)
      })
      .catch(() =>
        setError('No pudimos traer el listado de choferes. Volvé a intentar en unos minutos.'),
      )
  }, [filtros, pagina])

  useEffect(() => {
    traer()
  }, [traer])

  /** Cambiar cualquier filtro vuelve a la página 1: si no, se ve una página vacía (FR-030). */
  function actualizarFiltro<C extends keyof FiltrosChoferes>(campo: C, valor: FiltrosChoferes[C]) {
    setFiltros((previos) => ({ ...previos, [campo]: valor }))
    setPagina(1)
  }

  const filtrando =
    filtros.apellido.trim() !== '' ||
    filtros.dni.trim() !== '' ||
    filtros.transportistaId !== '' ||
    filtros.estado !== 'activo' ||
    filtros.estadoDocumentacion !== ''

  return (
    <main>
      <h1>Choferes</h1>

      <Link to="/choferes/nuevo">Nuevo chofer</Link>
      <Link to="/choferes/vencimientos">Ver vencimientos</Link>

      <section aria-label="Filtros" className="filtros">
        <div className="campo">
          <label htmlFor="filtro-apellido">Apellido</label>
          <input
            id="filtro-apellido"
            type="search"
            value={filtros.apellido}
            onChange={(evento) => actualizarFiltro('apellido', evento.target.value)}
          />
        </div>

        <div className="campo">
          <label htmlFor="filtro-dni">DNI</label>
          <input
            id="filtro-dni"
            type="search"
            value={filtros.dni}
            onChange={(evento) => actualizarFiltro('dni', evento.target.value)}
          />
        </div>

        <div className="campo">
          <label htmlFor="filtro-transportista">Transportista</label>
          <select
            id="filtro-transportista"
            value={filtros.transportistaId}
            onChange={(evento) =>
              actualizarFiltro(
                'transportistaId',
                evento.target.value === '' ? '' : Number(evento.target.value),
              )
            }
          >
            <option value="">Todos</option>
            {transportistas.map((transportista) => (
              <option key={transportista.id} value={transportista.id}>
                {transportista.nombre}
              </option>
            ))}
          </select>
        </div>

        <div className="campo">
          <label htmlFor="filtro-estado">Estado</label>
          <select
            id="filtro-estado"
            value={filtros.estado}
            onChange={(evento) =>
              actualizarFiltro('estado', evento.target.value as FiltrosChoferes['estado'])
            }
          >
            <option value="activo">Activo</option>
            <option value="inactivo">Inactivo</option>
          </select>
        </div>

        <div className="campo">
          <label htmlFor="filtro-estado-documentacion">Estado de documentación</label>
          <select
            id="filtro-estado-documentacion"
            value={filtros.estadoDocumentacion}
            onChange={(evento) =>
              actualizarFiltro(
                'estadoDocumentacion',
                evento.target.value as FiltrosChoferes['estadoDocumentacion'],
              )
            }
          >
            <option value="">Todos</option>
            {ESTADOS_DE_DOCUMENTACION.map((estado) => (
              <option key={estado.valor} value={estado.valor}>
                {estado.etiqueta}
              </option>
            ))}
          </select>
        </div>

        <button
          type="button"
          onClick={() => {
            setFiltros(FILTROS_CHOFERES_INICIALES)
            setPagina(1)
          }}
        >
          Limpiar filtros
        </button>
      </section>

      {error !== null && <p role="alert">{error}</p>}

      {resultado === null && error === null && <p role="status">Cargando choferes…</p>}

      {resultado !== null && resultado.items.length === 0 && (
        <p role="status">{filtrando ? MENSAJE_SIN_COINCIDENCIAS : MENSAJE_SIN_CHOFERES}</p>
      )}

      {resultado !== null && resultado.items.length > 0 && (
        <>
          <table>
            <caption>Choferes</caption>
            <thead>
              <tr>
                <th scope="col">Apellido y nombre</th>
                <th scope="col">DNI</th>
                <th scope="col">Transportista</th>
                <th scope="col">Estado</th>
                <th scope="col">Documentación</th>
                <th scope="col">Acciones</th>
              </tr>
            </thead>
            <tbody>
              {resultado.items.map((chofer) => (
                <tr key={chofer.id}>
                  <td>
                    {chofer.apellido}, {chofer.nombre}
                  </td>
                  <td>{chofer.dni}</td>
                  <td>{chofer.transportista.nombre}</td>
                  <td>{chofer.activo ? 'Activo' : 'Inactivo'}</td>
                  {/* El estado nunca se comunica sólo por color: el texto siempre acompaña. */}
                  <td className={claseDeEstado(chofer.estadoDocumentacion)}>
                    {TEXTO_ESTADO_CHOFER[chofer.estadoDocumentacion]}
                  </td>
                  <td>
                    <Link to={`/choferes/${chofer.id}`}>Ver ficha</Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          <Paginacion
            pagina={resultado.pagina}
            total={resultado.total}
            tamanioPagina={resultado.tamanioPagina}
            onCambiarPagina={setPagina}
            nombrePlural="choferes"
          />
        </>
      )}
    </main>
  )
}
