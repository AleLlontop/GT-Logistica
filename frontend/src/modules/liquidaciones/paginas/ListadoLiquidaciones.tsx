import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { formatearCuit } from '../../../compartido/cuit'
import { formatearPesos } from '../../../compartido/moneda'
import { Aviso } from '../../../compartido/ui/Aviso'
import { clasesDeBoton, clasesDeCirculoDeIcono } from '../../../compartido/ui/clases'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { Estado } from '../../../compartido/ui/Estado'
import { EstadoVacio } from '../../../compartido/ui/EstadoVacio'
import { EncabezadoDeChevron, FilaNavegable } from '../../../compartido/ui/FilaNavegable'
import { IconoNuevo } from '../../../compartido/ui/iconos'
import { Listado, TablaDesplazable } from '../../../compartido/ui/Listado'
import { Paginacion } from '../../../compartido/ui/Paginacion'
import { TokenDeIdentificador } from '../../../compartido/ui/TokenDeIdentificador'
import { FiltrosLiquidaciones } from '../componentes/FiltrosLiquidaciones'
import {
  ESTADO_EN_ORACION,
  FILTROS_LIQUIDACIONES_INICIALES,
  formatearPeriodo,
  listarLiquidaciones,
  NOMBRES_DE_ESTADO,
  type FiltrosLiquidaciones as ValorDeFiltros,
  type LiquidacionListado,
  type PaginaDe,
} from '../servicios/servicioLiquidaciones'

// Dos mensajes distintos a propósito: "todavía no hay ninguna" y "tu filtro no encontró nada" llevan a
// acciones distintas (contracts/README §Listado).
const MENSAJE_SIN_LIQUIDACIONES =
  'Todavía no se generó ninguna liquidación. Generá la primera eligiendo un transportista y un período.'
const MENSAJE_SIN_COINCIDENCIAS =
  'Ninguna liquidación coincide con los filtros aplicados. Probá limpiarlos.'

interface Props {
  /** `liquidaciones.gestionar`. Quien sólo consulta ve el listado sin el botón de generar (FR-065). */
  puedeGestionar: boolean
}

/**
 * Listado de liquidaciones (User Story 2, FR-021 a FR-024).
 *
 * **La columna *Resta pagar* es la que responde "cuánto se le debe a cada fletero"** sin abrir cada
 * liquidación: con pagos parciales el importe total ya no es lo que se debe. En una anulada no se debe
 * nada, y la celda lo dice con palabras.
 *
 * Sin filtro de estado **sí** aparecen las anuladas, atenuadas y con la palabra de su estado (FR-032), y
 * el control lo declara.
 */
export function ListadoLiquidaciones({ puedeGestionar }: Props) {
  const [filtros, setFiltros] = useState<ValorDeFiltros>(FILTROS_LIQUIDACIONES_INICIALES)
  const [pagina, setPagina] = useState(1)
  const [resultado, setResultado] = useState<PaginaDe<LiquidacionListado> | null>(null)
  const [error, setError] = useState<string | null>(null)

  const traer = useCallback(() => {
    listarLiquidaciones(filtros, pagina)
      .then((traida) => {
        setResultado(traida)
        setError(null)
      })
      .catch(() => setError('No pudimos traer las liquidaciones. Volvé a intentar en unos minutos.'))
  }, [filtros, pagina])

  useEffect(() => {
    traer()
  }, [traer])

  // Cualquier cambio de filtro vuelve a la página 1: quedarse en la 3 de un resultado con una sola
  // página muestra una tabla vacía que parece un error.
  function cambiarFiltros(nuevos: ValorDeFiltros) {
    setFiltros(nuevos)
    setPagina(1)
  }

  const filtrando =
    filtros.transportistaId !== '' || filtros.mes !== '' || filtros.anio !== '' || filtros.estado !== ''

  return (
    <section>
      <EncabezadoDePantalla
        titulo="Liquidaciones"
        accionPrincipal={
          puedeGestionar ? (
            <Link to="/liquidaciones/nueva" className={clasesDeBoton('primario')}>
              Generar liquidación
              <span aria-hidden="true" className={clasesDeCirculoDeIcono}>
                <IconoNuevo className="size-3" />
              </span>
            </Link>
          ) : undefined
        }
      />

      {error !== null && (
        <Aviso tono="error" rol="alert" className="mb-4">
          {error}
        </Aviso>
      )}

      <Listado>
        <FiltrosLiquidaciones
          valor={filtros}
          onCambio={cambiarFiltros}
          declaracion={
            <span role="status">
              {filtros.estado === ''
                ? 'Mostrando todas las liquidaciones, incluidas las anuladas.'
                : `Mostrando sólo las liquidaciones ${ESTADO_EN_ORACION[filtros.estado]}.`}
            </span>
          }
          resumen={
            resultado !== null && (
              <>
                <span>
                  {resultado.total} {resultado.total === 1 ? 'liquidación' : 'liquidaciones'}
                </span>
                <span>Ordenado por número, de la más nueva a la más vieja</span>
              </>
            )
          }
        />

        {resultado === null && error === null && (
          <EstadoVacio caso="cargando" className="border-0 shadow-none">
            Cargando liquidaciones…
          </EstadoVacio>
        )}

        {resultado !== null && resultado.items.length === 0 && (
          <EstadoVacio
            caso={filtrando ? 'sinCoincidencias' : 'vacio'}
            className="border-0 shadow-none"
          >
            {filtrando ? MENSAJE_SIN_COINCIDENCIAS : MENSAJE_SIN_LIQUIDACIONES}
          </EstadoVacio>
        )}

        {resultado !== null && resultado.items.length > 0 && (
          <TablaDesplazable>
            <table>
              <caption>Liquidaciones a transportistas</caption>
              <thead>
                <tr>
                  <th scope="col">Número</th>
                  <th scope="col">Período</th>
                  <th scope="col">Transportista</th>
                  <th scope="col" className="text-right">
                    Importe total
                  </th>
                  <th scope="col" className="text-right">
                    Resta pagar
                  </th>
                  <th scope="col">Estado</th>
                  <EncabezadoDeChevron />
                </tr>
              </thead>
              <tbody>
                {resultado.items.map((liquidacion) => (
                  // La anulada va atenuada **y** con la palabra de su estado: el color nunca comunica
                  // solo (FR-032). El token es el destino real de la fila (convención [008]).
                  <FilaNavegable
                    key={liquidacion.id}
                    a={`/liquidaciones/${liquidacion.id}`}
                    className={liquidacion.estado === 'anulada' ? 'atenuada' : undefined}
                  >
                    <td>
                      <TokenDeIdentificador
                        a={`/liquidaciones/${liquidacion.id}`}
                        numero={liquidacion.numero}
                        sinNumeral
                      />
                    </td>
                    <td>{formatearPeriodo(liquidacion.mes, liquidacion.anio)}</td>
                    <td>
                      <span className="flex flex-col gap-0.5">
                        <span className="font-semibold tracking-tight">
                          {liquidacion.transportista.razonSocial}
                          {!liquidacion.transportista.activo && (
                            <span className="ml-2 text-[11.5px] font-medium text-faint">Inactivo</span>
                          )}
                        </span>
                        <span className="font-mono text-[11.5px] text-faint">
                          {formatearCuit(liquidacion.transportista.cuit)}
                        </span>
                      </span>
                    </td>
                    <td className="text-right font-bold whitespace-nowrap">
                      {formatearPesos(liquidacion.importeTotal)}
                    </td>
                    <td className="text-right whitespace-nowrap">
                      {liquidacion.restaPagar === null ? (
                        <span className="text-faint">No corresponde</span>
                      ) : (
                        <span className="font-bold">{formatearPesos(liquidacion.restaPagar)}</span>
                      )}
                    </td>
                    <td>
                      <Estado
                        valor={liquidacion.estado}
                        texto={NOMBRES_DE_ESTADO[liquidacion.estado]}
                        forma="pastilla"
                        detalle={
                          liquidacion.estado === 'anulada' && liquidacion.motivoAnulacion !== null
                            ? liquidacion.motivoAnulacion
                            : undefined
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
          nombrePlural="liquidaciones"
          onCambiarPagina={setPagina}
        />
      )}
    </section>
  )
}
