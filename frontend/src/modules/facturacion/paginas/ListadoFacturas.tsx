import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { Aviso } from '../../../compartido/ui/Aviso'
import { clasesDeBoton } from '../../../compartido/ui/clases'
import { Estado } from '../../../compartido/ui/Estado'
import { EstadoVacio } from '../../../compartido/ui/EstadoVacio'
import { Listado, TablaDesplazable } from '../../../compartido/ui/Listado'
import { clasesDeEnlaceDeFila } from '../../../compartido/ui/clases'
import { useCallback, useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { formatearFecha } from '../../../compartido/fechas'
import { formatearPesos } from '../../../compartido/moneda'
import { FiltrosFacturas } from '../componentes/FiltrosFacturas'
import { Paginacion } from '../../../compartido/ui/Paginacion'
import {
  ESTADO_EN_ORACION,
  NOMBRES_DE_ESTADO,
  NOMBRES_DE_TIPO_COMPROBANTE,
  type PaginaDe,
} from '../servicios/api'
import {
  FILTROS_FACTURAS_INICIALES,
  listarFacturas,
  nombreDeCliente,
  type FacturaListado,
} from '../servicios/servicioFacturas'

// Dos mensajes distintos a propósito: "todavía no emitiste ninguna" y "tu filtro no encontró nada" son
// situaciones distintas y llevan a acciones distintas (contracts/README.md).
const MENSAJE_SIN_FACTURAS =
  'Todavía no se emitió ninguna factura. Emití la primera para empezar a seguir la cobranza.'
const MENSAJE_SIN_COINCIDENCIAS = 'Ninguna factura coincide con los filtros aplicados.'

interface Props {
  /** `facturacion.gestionar`. Quien sólo consulta ve el listado sin el botón de emitir (FR-068). */
  puedeGestionar: boolean
}

/**
 * Listado de facturas (User Story 3).
 *
 * **Las ocho columnas de FR-057**, con el estado **siempre en palabras** y nunca sólo por color (FR-065).
 * Cada estado suma el dato que lo explica: la vencida dice hace cuántos días venció, la pagada su fecha de
 * cobro, y la anulada su motivo con la fila atenuada.
 *
 * **El control de estado dice qué está mostrando**, y a diferencia del listado de viajes, sin filtro **sí**
 * incluye las anuladas: una factura anulada sigue siendo parte de la historia de cobranza (FR-064).
 *
 * Los importes van con `formatearPesos` y las fechas con `formatearFecha`, nunca a mano (Principio II,
 * convenciones [003] y [005]).
 */
export function ListadoFacturas({ puedeGestionar }: Props) {
  const navegar = useNavigate()

  const [filtros, setFiltros] = useState(FILTROS_FACTURAS_INICIALES)
  const [pagina, setPagina] = useState(1)
  const [resultado, setResultado] = useState<PaginaDe<FacturaListado> | null>(null)
  const [error, setError] = useState<string | null>(null)

  const traer = useCallback(() => {
    listarFacturas(filtros, pagina)
      .then((pagina) => {
        setResultado(pagina)
        setError(null)
      })
      .catch(() => setError('No pudimos traer las facturas. Volvé a intentar en unos minutos.'))
  }, [filtros, pagina])

  useEffect(() => {
    traer()
  }, [traer])

  // Cualquier cambio de filtro vuelve a la primera página: quedarse en la 3 de un resultado que ahora
  // tiene una sola página muestra una tabla vacía que parece un error.
  function cambiarFiltros(nuevos: typeof filtros) {
    setFiltros(nuevos)
    setPagina(1)
  }

  const filtrando =
    filtros.clienteId !== '' ||
    filtros.desde !== '' ||
    filtros.hasta !== '' ||
    filtros.mes !== '' ||
    filtros.anio !== '' ||
    filtros.estado !== '' ||
    filtros.tipoComprobante !== ''

  return (
    <section>
      <EncabezadoDePantalla
        titulo="Facturas"
        accionPrincipal={
          puedeGestionar && (
            <Link to="/facturas/nueva" className={clasesDeBoton('primario')}>
              Nueva factura
            </Link>
          )
        }
      />

      {error !== null && (
        <Aviso tono="error" rol="alert" className="mb-4">
          {error}
        </Aviso>
      )}

      <Listado>
        <FiltrosFacturas valor={filtros} onCambio={cambiarFiltros} />

        {/* El control nunca oculta filas en silencio: si no se eligió estado, dice qué está mostrando
            (FR-064). */}
        <p role="status" className="border-b border-borde px-4 py-2 text-sm text-texto-suave">
          {filtros.estado === ''
            ? 'Mostrando todas las facturas, incluidas las anuladas.'
            : `Mostrando sólo las facturas ${ESTADO_EN_ORACION[filtros.estado]}.`}
        </p>

        {resultado === null && error === null && (
          <EstadoVacio caso="cargando" className="border-0 shadow-none">
            Cargando facturas…
          </EstadoVacio>
        )}

        {resultado !== null && resultado.items.length === 0 && (
          <EstadoVacio
            caso={filtrando ? 'sinCoincidencias' : 'vacio'}
            className="border-0 shadow-none"
          >
            {filtrando ? MENSAJE_SIN_COINCIDENCIAS : MENSAJE_SIN_FACTURAS}
          </EstadoVacio>
        )}

        {resultado !== null && resultado.items.length > 0 && (
          <TablaDesplazable>
        <table>
          <caption>Facturas emitidas</caption>
          <thead>
            <tr>
              <th scope="col">Número</th>
              <th scope="col">Fecha</th>
              <th scope="col">Cliente</th>
              <th scope="col">Tipo</th>
              <th scope="col">Período</th>
              <th scope="col" className="text-right">
                Total
              </th>
              <th scope="col">Estado</th>
              <th scope="col">Vencimiento de pago</th>
            </tr>
          </thead>
          <tbody>
            {resultado.items.map((factura) => (
              // La fila anulada va atenuada **y** con la palabra que lo explica en la columna de
              // estado: un elemento atenuado nunca comunica sólo con el color (FR-065).
              <tr
                key={factura.id}
                className={factura.estado === 'anulada' ? 'atenuada' : undefined}
              >
                <td>
                  <button
                    type="button"
                    onClick={() => navegar(`/facturas/${factura.id}`)}
                    className={clasesDeEnlaceDeFila()}
                  >
                    {factura.numeroComprobante}
                  </button>
                </td>
                <td>{formatearFecha(factura.fecha)}</td>
                <td>{nombreDeCliente(factura.cliente)}</td>
                <td>{NOMBRES_DE_TIPO_COMPROBANTE[factura.tipoComprobante]}</td>
                <td>
                  {String(factura.mes).padStart(2, '0')}/{factura.anio}
                </td>
                <td className="text-right font-medium">{formatearPesos(factura.total)}</td>
                <td>
                  <Estado valor={factura.estado} texto={NOMBRES_DE_ESTADO[factura.estado]} />
                  {/* Cada estado suma el dato que lo explica (contracts/README §Listado). */}
                  {factura.estado === 'vencida' && (
                    <span className="block text-xs text-texto-suave">
                      {' '}
                      — Venció hace {diasDesde(factura.vencimientoPago)} días
                    </span>
                  )}
                  {factura.estado === 'pagada' && factura.fechaCobro !== null && (
                    <span className="block text-xs text-texto-suave">
                      {' '}
                      — Cobrada el {formatearFecha(factura.fechaCobro)}
                    </span>
                  )}
                  {factura.estado === 'anulada' && factura.motivoAnulacion !== null && (
                    <span className="block text-xs text-texto-suave"> — {factura.motivoAnulacion}</span>
                  )}
                </td>
                <td>{formatearFecha(factura.vencimientoPago)}</td>
              </tr>
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
          nombrePlural="facturas"
          onCambiarPagina={setPagina}
        />
      )}
    </section>
  )
}

/**
 * Días corridos desde una fecha `yyyy-MM-dd` hasta hoy.
 *
 * Se construye con los tres números y no con `new Date(iso)`: eso interpreta la cadena como medianoche
 * UTC y en UTC−3 devuelve el día anterior (convención [003]).
 */
function diasDesde(iso: string): number {
  const [anio, mes, dia] = iso.split('-').map(Number)
  const vencimiento = new Date(anio, mes - 1, dia)
  const hoy = new Date()

  const soloDia = new Date(hoy.getFullYear(), hoy.getMonth(), hoy.getDate())

  return Math.round((soloDia.getTime() - vencimiento.getTime()) / 86_400_000)
}
