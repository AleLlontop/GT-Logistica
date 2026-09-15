import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { formatearFecha } from '../../../compartido/fechas'
import { formatearPesos } from '../../../compartido/moneda'
import { clasesDeAvisoDePantalla, clasesDeBoton, clasesDeCirculoDeIcono } from '../../../compartido/ui/clases'
import { cn } from '../../../compartido/ui/cn'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { EnlaceDeFila } from '../../../compartido/ui/EnlaceDeFila'
import { Estado } from '../../../compartido/ui/Estado'
import { EstadoVacio } from '../../../compartido/ui/EstadoVacio'
import { EncabezadoDeChevron, FilaNavegable } from '../../../compartido/ui/FilaNavegable'
import { IconoNuevo } from '../../../compartido/ui/iconos'
import { Listado, TablaDesplazable } from '../../../compartido/ui/Listado'
import { Paginacion } from '../../../compartido/ui/Paginacion'
import { FiltrosAdelantos } from '../componentes/FiltrosAdelantos'
import {
  ESTADO_EN_ORACION,
  esSinPermiso,
  FILTROS_ADELANTOS_INICIALES,
  formatearPersona,
  listarAdelantos,
  NOMBRES_DE_ESTADO,
  rangoInvertido,
  type FiltrosAdelantos as ValorDeFiltros,
  type PaginaDeAdelantos,
} from '../servicios/servicioAdelantos'

const TITULO = 'Adelantos de sueldo'

export const MENSAJE_SIN_PERMISO_PARA_VER =
  'No tenés permiso para ver los adelantos. Si lo necesitás, pedíselo a quien administra los usuarios del sistema.'

const MENSAJE_SIN_COINCIDENCIAS = 'Ningún adelanto coincide con los filtros aplicados. Probá limpiarlos.'

interface Props {
  /** `adelantos.gestionar`. Quien sólo consulta ve el listado sin el botón de registrar (FR-043). */
  puedeGestionar: boolean
}

/**
 * Consultar adelantos (User Story 2, FR-013 a FR-018).
 *
 * **El total adelantado es lo que responde "cuánto se le adelantó a una persona en el mes"** sin sumar a
 * mano: lo calcula el servidor sobre toda la selección y sólo con los aprobados (FR-016).
 *
 * Sin filtro de estado **sí** aparecen rechazados y anulados, atenuados y con la palabra de su estado
 * (FR-039), y el control lo declara.
 *
 * **Quien llega sin permiso escribiendo la dirección** ve que le falta permiso y a quién pedírselo, no un
 * error de carga que invita a reintentar: lo decide el `403` de la carga (FR-043, research §7).
 */
export function ListadoAdelantos({ puedeGestionar }: Props) {
  const [filtros, setFiltros] = useState<ValorDeFiltros>(FILTROS_ADELANTOS_INICIALES)
  const [pagina, setPagina] = useState(1)
  const [resultado, setResultado] = useState<PaginaDeAdelantos | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [sinPermiso, setSinPermiso] = useState(false)

  const invertido = rangoInvertido(filtros)

  const traer = useCallback(() => {
    // FR-015: con el rango invertido no se consulta.
    if (rangoInvertido(filtros)) {
      return
    }

    listarAdelantos(filtros, pagina)
      .then((traida) => {
        setResultado(traida)
        setError(null)
      })
      .catch((fallo) => {
        if (esSinPermiso(fallo)) {
          setSinPermiso(true)
        } else {
          setError('No pudimos traer los adelantos. Volvé a intentar en unos minutos.')
        }
      })
  }, [filtros, pagina])

  useEffect(() => {
    traer()
  }, [traer])

  // Cualquier cambio de filtro vuelve a la página 1.
  function cambiarFiltros(nuevos: ValorDeFiltros) {
    setFiltros(nuevos)
    setPagina(1)
  }

  if (sinPermiso) {
    return (
      <section>
        <EncabezadoDePantalla titulo={TITULO} />
        <p role="alert" className={clasesDeAvisoDePantalla.advertencia}>
          {MENSAJE_SIN_PERMISO_PARA_VER}
        </p>
      </section>
    )
  }

  const filtrando =
    filtros.personaId !== '' || filtros.desde !== '' || filtros.hasta !== '' || filtros.estado !== ''

  const visible = invertido ? null : resultado

  return (
    <section>
      <EncabezadoDePantalla
        titulo={TITULO}
        accionPrincipal={
          puedeGestionar ? (
            <Link to="/adelantos/nuevo" className={clasesDeBoton('primario')}>
              Registrar adelanto
              <span aria-hidden="true" className={clasesDeCirculoDeIcono}>
                <IconoNuevo className="size-3" />
              </span>
            </Link>
          ) : undefined
        }
      />

      {error !== null && (
        <p role="alert" className={cn(clasesDeAvisoDePantalla.error, 'mb-4')}>
          {error}
        </p>
      )}

      <Listado>
        <FiltrosAdelantos
          valor={filtros}
          onCambio={cambiarFiltros}
          declaracion={
            <span role="status">
              {filtros.estado === ''
                ? 'Mostrando todos los adelantos, incluidos los rechazados y los anulados.'
                : `Mostrando sólo los adelantos ${ESTADO_EN_ORACION[filtros.estado]}.`}
            </span>
          }
          resumen={
            visible !== null && (
              <>
                <span>
                  {visible.total} {visible.total === 1 ? 'adelanto' : 'adelantos'}
                </span>
                <span className="text-ink">
                  Total adelantado <strong className="whitespace-nowrap">{formatearPesos(visible.totalAdelantado)}</strong>
                </span>
                <span>Suma sólo los aprobados</span>
                <span>Ordenado por fecha, del más reciente al más antiguo</span>
              </>
            )
          }
        />

        {invertido && (
          <EstadoVacio caso="sinCoincidencias" className="border-0 shadow-none">
            Corregí el rango de fechas para ver los adelantos.
          </EstadoVacio>
        )}

        {!invertido && resultado === null && error === null && (
          <EstadoVacio caso="cargando" className="border-0 shadow-none">
            Cargando adelantos…
          </EstadoVacio>
        )}

        {visible !== null && visible.items.length === 0 && (
          <EstadoVacio caso={filtrando ? 'sinCoincidencias' : 'vacio'} className="border-0 shadow-none">
            {filtrando
              ? MENSAJE_SIN_COINCIDENCIAS
              : `Todavía no se registró ningún adelanto.${
                  puedeGestionar ? ' Registrá el primero eligiendo a la persona y el importe.' : ''
                }`}
          </EstadoVacio>
        )}

        {visible !== null && visible.items.length > 0 && (
          <TablaDesplazable>
            <table>
              <caption>{TITULO}</caption>
              <thead>
                <tr>
                  <th scope="col">Fecha</th>
                  <th scope="col">Persona</th>
                  <th scope="col">Motivo</th>
                  <th scope="col" className="text-right">
                    Importe
                  </th>
                  <th scope="col">Estado</th>
                  <EncabezadoDeChevron />
                </tr>
              </thead>
              <tbody>
                {visible.items.map((adelanto) => {
                  const destino = `/adelantos/${adelanto.id}`
                  const fecha = formatearFecha(adelanto.fecha)

                  return (
                    // Rechazado y anulado van atenuados **y** con la palabra de su estado (FR-039).
                    <FilaNavegable
                      key={adelanto.id}
                      a={destino}
                      className={
                        adelanto.estado === 'rechazado' || adelanto.estado === 'anulado' ? 'atenuada' : undefined
                      }
                    >
                      <td className="whitespace-nowrap">{fecha}</td>
                      <td>
                        {/* Una persona tiene varias filas: el nombre accesible suma la fecha para que
                            veinte enlaces no se lean igual, y contiene el texto visible (contracts/README
                            §Listado). Va entero en un solo `sr-only`: partido en varios, el cálculo del
                            nombre recorta los espacios de borde de cada uno y pega las palabras. */}
                        <EnlaceDeFila a={destino} identificador={adelanto.persona.dni}>
                          <span className="sr-only">{`Ver adelanto de ${formatearPersona(adelanto.persona)} del ${fecha}`}</span>
                          <span aria-hidden="true">{formatearPersona(adelanto.persona)}</span>
                        </EnlaceDeFila>
                      </td>
                      <td>
                        <span className="block max-w-[18rem] truncate">{adelanto.motivo}</span>
                      </td>
                      <td className="text-right font-bold whitespace-nowrap">{formatearPesos(adelanto.importe)}</td>
                      <td>
                        <Estado valor={adelanto.estado} texto={NOMBRES_DE_ESTADO[adelanto.estado]} forma="pastilla" />
                      </td>
                    </FilaNavegable>
                  )
                })}
              </tbody>
            </table>
          </TablaDesplazable>
        )}
      </Listado>

      {visible !== null && (
        <Paginacion
          pagina={visible.pagina}
          total={visible.total}
          tamanioPagina={visible.tamanioPagina}
          nombrePlural="adelantos"
          onCambiarPagina={setPagina}
        />
      )}
    </section>
  )
}
