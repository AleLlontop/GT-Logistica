import { useCallback, useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { formatearFecha } from '../../../compartido/fechas'
import { formatearPesos } from '../../../compartido/moneda'
import { FiltrosFacturas } from '../componentes/FiltrosFacturas'
import { Paginacion } from '../componentes/Paginacion'
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
    <main>
      <h1>Facturas</h1>

      {puedeGestionar && <Link to="/facturas/nueva">Nueva factura</Link>}

      <FiltrosFacturas valor={filtros} onCambio={cambiarFiltros} />

      {/* El control nunca oculta filas en silencio: si no se eligió estado, dice qué está mostrando
          (FR-064). */}
      <p role="status">
        {filtros.estado === ''
          ? 'Mostrando todas las facturas, incluidas las anuladas.'
          : `Mostrando sólo las facturas ${ESTADO_EN_ORACION[filtros.estado]}.`}
      </p>

      {error !== null && <p role="alert">{error}</p>}

      {resultado === null && error === null && <p role="status">Cargando facturas…</p>}

      {resultado !== null && resultado.items.length === 0 && (
        <p role="status">{filtrando ? MENSAJE_SIN_COINCIDENCIAS : MENSAJE_SIN_FACTURAS}</p>
      )}

      {resultado !== null && resultado.items.length > 0 && (
        <table>
          <caption>Facturas emitidas</caption>
          <thead>
            <tr>
              <th scope="col">Número</th>
              <th scope="col">Fecha</th>
              <th scope="col">Cliente</th>
              <th scope="col">Tipo</th>
              <th scope="col">Período</th>
              <th scope="col">Total</th>
              <th scope="col">Estado</th>
              <th scope="col">Vencimiento de pago</th>
            </tr>
          </thead>
          <tbody>
            {resultado.items.map((factura) => (
              // La fila anulada va atenuada **y** con la palabra que lo explica en la columna de
              // estado: un elemento atenuado nunca comunica sólo con el color (FR-065).
              <tr key={factura.id} className={factura.estado === 'anulada' ? 'atenuada' : undefined}>
                <td>
                  <button type="button" onClick={() => navegar(`/facturas/${factura.id}`)}>
                    {factura.numeroComprobante}
                  </button>
                </td>
                <td>{formatearFecha(factura.fecha)}</td>
                <td>{nombreDeCliente(factura.cliente)}</td>
                <td>{NOMBRES_DE_TIPO_COMPROBANTE[factura.tipoComprobante]}</td>
                <td>
                  {String(factura.mes).padStart(2, '0')}/{factura.anio}
                </td>
                <td>{formatearPesos(factura.total)}</td>
                <td>
                  {NOMBRES_DE_ESTADO[factura.estado]}
                  {/* Cada estado suma el dato que lo explica (contracts/README §Listado). */}
                  {factura.estado === 'vencida' && (
                    <span> — Venció hace {diasDesde(factura.vencimientoPago)} días</span>
                  )}
                  {factura.estado === 'pagada' && factura.fechaCobro !== null && (
                    <span> — Cobrada el {formatearFecha(factura.fechaCobro)}</span>
                  )}
                  {factura.estado === 'anulada' && factura.motivoAnulacion !== null && (
                    <span> — {factura.motivoAnulacion}</span>
                  )}
                </td>
                <td>{formatearFecha(factura.vencimientoPago)}</td>
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
          entidad="facturas"
          onCambiarPagina={setPagina}
        />
      )}
    </main>
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
