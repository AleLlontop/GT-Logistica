import { Estado } from '../../../compartido/ui/Estado'
import { Aviso } from '../../../compartido/ui/Aviso'
import { Boton } from '../../../compartido/ui/Boton'
import { EnlaceDeFila } from '../../../compartido/ui/EnlaceDeFila'
import { EstadoVacio } from '../../../compartido/ui/EstadoVacio'
import { EncabezadoDeChevron, FilaNavegable } from '../../../compartido/ui/FilaNavegable'
import { Filtros, FranjaDeBusqueda, FranjaDeFiltros } from '../../../compartido/ui/Filtros'
import { Listado, TablaDesplazable } from '../../../compartido/ui/Listado'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import {
  clasesDeBoton,
  clasesDeEtiquetaDeFiltro,
  clasesDeFiltro,
} from '../../../compartido/ui/clases'
import { IconoNuevo } from '../../../compartido/ui/iconos'
import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { Paginacion } from '../../../compartido/ui/Paginacion'
import { TEXTO_ESTADO_CHOFER } from '../servicios/estados'
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
    <section>
      <EncabezadoDePantalla
        titulo="Choferes"
        accionPrincipal={
          <>
            {/*
              Las dos son enlaces porque navegan (FR-023). *Ver vencimientos* es una de las **dos
              acciones secundarias reales** del sistema —la otra es la de flota—: acompaña al
              primario sin competir con él (FR-019).
            */}
            <Link to="/choferes/vencimientos" className={clasesDeBoton('secundario')}>
              Ver vencimientos
            </Link>
            <Link to="/choferes/nuevo" className={clasesDeBoton('primario')}>
              Nuevo chofer
              <span
                aria-hidden="true"
                className="flex size-8 shrink-0 items-center justify-center rounded-pastilla bg-white/[0.14] transition-transform duration-200 ease-gt group-hover:translate-x-0.5"
              >
                <IconoNuevo className="size-3" />
              </span>
            </Link>
          </>
        }
      />

      {error !== null && (
        <Aviso tono="error" rol="alert" className="mb-[18px]">
          {error}
        </Aviso>
      )}

      {/*
        Los filtros viven **adentro** de la isla del listado, no como un formulario suelto encima de
        la tabla (FR-032). Y la isla se dibuja siempre, también cuando no hay filas: si los filtros
        desaparecieran junto con la tabla, no habría forma de limpiarlos.
      */}
      <Listado>
        <Filtros
          resumen={
            resultado !== null && (
              <>
                <span>
                  {resultado.total} {resultado.total === 1 ? 'chofer' : 'choferes'}
                </span>
                <span>Ordenado por apellido y nombre</span>
              </>
            )
          }
        >
          {/* El buscador primero y a todo el ancho. Acá son dos: apellido y DNI (FR-033). */}
          <FranjaDeBusqueda>
            <div className="flex flex-col gap-1.5">
              <label htmlFor="filtro-apellido" className={clasesDeEtiquetaDeFiltro}>
                Apellido
              </label>
              <input
                id="filtro-apellido"
                type="search"
                placeholder="Gómez"
                value={filtros.apellido}
                onChange={(evento) => actualizarFiltro('apellido', evento.target.value)}
                className={clasesDeFiltro(filtros.apellido.trim() !== '')}
              />
            </div>

            <div className="flex flex-col gap-1.5">
              <label htmlFor="filtro-dni" className={clasesDeEtiquetaDeFiltro}>
                DNI
              </label>
              <input
                id="filtro-dni"
                type="search"
                placeholder="30.123.456"
                value={filtros.dni}
                onChange={(evento) => actualizarFiltro('dni', evento.target.value)}
                className={clasesDeFiltro(filtros.dni.trim() !== '')}
              />
            </div>
          </FranjaDeBusqueda>

          <FranjaDeFiltros>
            <div className="flex flex-col gap-1.5">
              <label htmlFor="filtro-transportista" className={clasesDeEtiquetaDeFiltro}>
                Transportista
              </label>
              <select
                id="filtro-transportista"
                value={filtros.transportistaId}
                onChange={(evento) =>
                  actualizarFiltro(
                    'transportistaId',
                    evento.target.value === '' ? '' : Number(evento.target.value),
                  )
                }
                className={clasesDeFiltro(filtros.transportistaId !== '')}
              >
                <option value="">Todos</option>
                {transportistas.map((transportista) => (
                  <option key={transportista.id} value={transportista.id}>
                    {transportista.nombre}
                  </option>
                ))}
              </select>
            </div>

            <div className="flex flex-col gap-1.5">
              <label htmlFor="filtro-estado" className={clasesDeEtiquetaDeFiltro}>
                Estado
              </label>
              {/*
                Arranca en `Activo`, **a la vista**: un listado que oculta choferes sin decirlo se
                lee como un error de datos (FR-022 del Módulo 3, convención [003]).
              */}
              <select
                id="filtro-estado"
                value={filtros.estado}
                onChange={(evento) =>
                  actualizarFiltro('estado', evento.target.value as FiltrosChoferes['estado'])
                }
                className={clasesDeFiltro(filtros.estado !== 'activo')}
              >
                <option value="activo">Activo</option>
                <option value="inactivo">Inactivo</option>
              </select>
            </div>

            <div className="flex flex-col gap-1.5">
              <label htmlFor="filtro-estado-documentacion" className={clasesDeEtiquetaDeFiltro}>
                Estado de documentación
              </label>
              <select
                id="filtro-estado-documentacion"
                value={filtros.estadoDocumentacion}
                onChange={(evento) =>
                  actualizarFiltro(
                    'estadoDocumentacion',
                    evento.target.value as FiltrosChoferes['estadoDocumentacion'],
                  )
                }
                className={clasesDeFiltro(filtros.estadoDocumentacion !== '')}
              >
                <option value="">Todos</option>
                {ESTADOS_DE_DOCUMENTACION.map((estado) => (
                  <option key={estado.valor} value={estado.valor}>
                    {estado.etiqueta}
                  </option>
                ))}
              </select>
            </div>

            <div className="flex flex-col gap-1.5">
              <label className={clasesDeEtiquetaDeFiltro + ' invisible'} aria-hidden="true">
                Limpiar
              </label>
              <Boton
                variante="secundario"
                tamanio="chico"
                onClick={() => {
                  setFiltros(FILTROS_CHOFERES_INICIALES)
                  setPagina(1)
                }}
              >
                Limpiar filtros
              </Boton>
            </div>
          </FranjaDeFiltros>
        </Filtros>

        {resultado === null && error === null && (
          <EstadoVacio caso="cargando" className="border-0 shadow-none">
            Cargando choferes…
          </EstadoVacio>
        )}

        {resultado !== null && resultado.items.length === 0 && (
          <EstadoVacio
            caso={filtrando ? 'sinCoincidencias' : 'vacio'}
            className="border-0 shadow-none"
          >
            {filtrando ? MENSAJE_SIN_COINCIDENCIAS : MENSAJE_SIN_CHOFERES}
          </EstadoVacio>
        )}

        {resultado !== null && resultado.items.length > 0 && (
          <TablaDesplazable>
            <table>
              <caption>Choferes</caption>
              <thead>
                <tr>
                  {/* `Apellido y nombre` + `DNI` nombran un solo concepto: el chofer (FR-047). */}
                  <th scope="col">Chofer</th>
                  <th scope="col">Transportista</th>
                  <th scope="col">Estado</th>
                  <th scope="col">Documentación</th>
                  {/*
                    La columna `Acciones` **desaparece y no deja ningún `···`**: contenía sólo
                    *Ver ficha*, que es exactamente lo que pasa a ser el enlace de fila (FR-046).
                  */}
                  <EncabezadoDeChevron />
                </tr>
              </thead>
              <tbody>
                {resultado.items.map((chofer) => (
                  <FilaNavegable key={chofer.id} a={`/choferes/${chofer.id}`}>
                    <td>
                      <EnlaceDeFila a={`/choferes/${chofer.id}`} identificador={chofer.dni}>
                        {chofer.apellido}, {chofer.nombre}
                      </EnlaceDeFila>
                    </td>
                    <td>{chofer.transportista.nombre}</td>
                    {/*
                      Alta y baja **acompañan**: van como punto para no competir con el semáforo de
                      documentación, que es el estado del que trata la pantalla (FR-055).
                    */}
                    <td>
                      <Estado
                        valor={chofer.activo ? 'activo' : 'inactivo'}
                        texto={chofer.activo ? 'Activo' : 'Inactivo'}
                        forma="punto"
                      />
                    </td>
                    {/* El estado nunca se comunica sólo por color: el texto siempre acompaña. */}
                    <td>
                      <Estado
                        valor={chofer.estadoDocumentacion}
                        texto={TEXTO_ESTADO_CHOFER[chofer.estadoDocumentacion]}
                        forma="pastilla"
                      />
                    </td>
                  </FilaNavegable>
                ))}
              </tbody>
            </table>
          </TablaDesplazable>
        )}
      </Listado>

      {resultado !== null && resultado.items.length > 0 && (
        <Paginacion
          pagina={resultado.pagina}
          total={resultado.total}
          tamanioPagina={resultado.tamanioPagina}
          onCambiarPagina={setPagina}
          nombrePlural="choferes"
        />
      )}
    </section>
  )
}
