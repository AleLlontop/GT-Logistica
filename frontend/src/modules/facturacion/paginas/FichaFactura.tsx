import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { useCallback, useEffect, useState } from 'react'
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { formatearFecha, formatearInstante } from '../../../compartido/fechas'
import { formatearPesos } from '../../../compartido/moneda'
import { ConfirmacionAnulacion } from '../componentes/ConfirmacionAnulacion'
import { RegistrarCobro } from '../componentes/RegistrarCobro'
import {
  NOMBRES_DE_CONDICION_DE_VENTA,
  NOMBRES_DE_ESTADO,
  NOMBRES_DE_TIPO_COMPROBANTE,
  NOMBRES_DE_TIPO_FACTURACION,
} from '../servicios/api'
import {
  anularFactura,
  nombreDeCliente,
  obtenerFactura,
  registrarCobro,
  type FacturaDetalle,
} from '../servicios/servicioFacturas'

export const AVISO_DATOS_CONGELADOS =
  'Estos datos son los que tenía la factura el día que se emitió. Un cambio posterior en la ' +
  'configuración o en el padrón no la modifica.'

export const NOTA_DEL_DOCUMENTO =
  'Este documento es la representación impresa de la factura, no el comprobante fiscal. La validez la ' +
  'da el CAE, que se obtiene en AFIP/ARCA por fuera del sistema.'

interface Props {
  /** `facturacion.gestionar`: corregir y registrar el cobro (FR-067). */
  puedeGestionar: boolean
  /** `facturacion.anular`, que es un permiso aparte y sólo del administrador (FR-067). */
  puedeAnular: boolean
}

/**
 * Ficha completa de una factura (User Story 3, FR-060).
 *
 * **El aviso de datos congelados es permanente**, no un tooltip: la ficha muestra a quién se le facturó
 * ese día, no quién es hoy, y quien mira tiene que saberlo sin tener que descubrirlo (FR-034, FR-034a).
 *
 * **La pantalla ofrece exactamente las acciones que el estado y el permiso admiten y ninguna más**
 * (contracts/README §Acciones). En `anulada` no hay ninguna, y **no existe ninguna acción para revertir un
 * cobro ni para devolver una anulada a `pendiente`**: no están ocultas, no existen (FR-043, FR-038).
 *
 * **Una entrada del historial sin estado nuevo se lee `Corrección de datos`**: el sistema registra quién y
 * cuándo, y no qué campos cambiaron (FR-037).
 */
export function FichaFactura({ puedeGestionar, puedeAnular }: Props) {
  const { id } = useParams()
  const navegar = useNavigate()
  const facturaId = Number(id)

  const ubicacion = useLocation()

  // La emisión confirma acá, porque el guardado ocurrió en el alta y la navegación se llevó el resultado
  // con ella (FR-014, convención [005]).
  const estadoDeNavegacion = ubicacion.state as { aviso?: string } | null

  const [factura, setFactura] = useState<FacturaDetalle | null>(null)
  const [aviso, setAviso] = useState<string | null>(estadoDeNavegacion?.aviso ?? null)
  const [error, setError] = useState<string | null>(null)
  const [trabajando, setTrabajando] = useState(false)
  const [cobrando, setCobrando] = useState(false)
  const [anulando, setAnulando] = useState(false)

  const traer = useCallback(() => {
    obtenerFactura(facturaId)
      .then((traida) => {
        setFactura(traida)
        setError(null)
      })
      .catch(() => setError('No pudimos traer la factura. Volvé a intentar en unos minutos.'))
  }, [facturaId])

  useEffect(() => {
    traer()
  }, [traer])

  function mostrarFallo(fallo: unknown) {
    setError(
      fallo instanceof ErrorHttp
        ? fallo.detalle.mensaje
        : 'Ocurrió un problema inesperado. Volvé a intentar en unos minutos.',
    )
  }

  async function cobrar(fechaCobro: string) {
    setTrabajando(true)
    setError(null)
    setAviso(null)

    try {
      const actualizada = await registrarCobro(facturaId, fechaCobro)

      setFactura(actualizada)
      setCobrando(false)
      setAviso(`Se registró el cobro de la factura ${actualizada.numeroComprobante}.`)
    } catch (fallo) {
      mostrarFallo(fallo)
    } finally {
      setTrabajando(false)
    }
  }

  async function anular(motivo: string) {
    setTrabajando(true)
    setError(null)
    setAviso(null)

    // La cuenta se toma **antes** de anular: la anulación les pone `FacturaId` en nulo y los devuelve a
    // `rendido`, así que la factura releída ya no los incluye —y eso es el dato correcto, no un error—.
    // El detalle de qué viajes tenía queda en el documento regenerado (data-model §Anular).
    const cuantosVuelven = factura?.viajes.length ?? 0

    try {
      const actualizada = await anularFactura(facturaId, motivo)

      setFactura(actualizada)
      setAnulando(false)
      setAviso(
        `Se anuló la factura ${actualizada.numeroComprobante}. Sus ${cuantosVuelven} viajes ` +
          'volvieron a estado rendido y quedan disponibles para facturar de nuevo.',
      )
    } catch (fallo) {
      setAnulando(false)
      mostrarFallo(fallo)
    } finally {
      setTrabajando(false)
    }
  }

  if (factura === null) {
    return (
      <section>
        <EncabezadoDePantalla titulo="Factura" />
        {error !== null ? <p role="alert">{error}</p> : <p role="status">Cargando…</p>}
      </section>
    )
  }

  // Las acciones que cada estado admite (contracts/README §Acciones).
  const admiteCobroYAnulacion = factura.estado === 'pendiente' || factura.estado === 'vencida'
  const admiteCorreccion = admiteCobroYAnulacion || factura.estado === 'pagada'

  return (
    <section className="flex flex-col gap-4 [&>section]:rounded-medio [&>section]:border [&>section]:border-borde [&>section]:bg-superficie [&>section]:shadow-tarjeta [&>section>h2]:m-0 [&>section>h2]:border-b [&>section>h2]:border-borde [&>section>h2]:px-5 [&>section>h2]:py-3 [&>section>h2]:text-sm [&>section>h2]:font-semibold [&>section>h2]:uppercase [&>section>h2]:tracking-wide [&>section>h2]:text-texto-suave [&_dl]:m-0 [&_dl]:grid [&_dl]:grid-cols-[minmax(10rem,auto)_1fr] [&_dl]:gap-x-6 [&_dl]:gap-y-2 [&_dl]:px-5 [&_dl]:py-4 [&_dt]:text-sm [&_dt]:text-texto-suave [&_dd]:m-0 [&_dd]:text-sm [&_dd]:font-medium [&_dd]:text-texto [&_table]:w-full [&_table]:border-collapse [&_table]:text-sm [&_caption]:sr-only [&_thead]:bg-superficie-hundida [&_th]:border-b [&_th]:border-borde-fuerte [&_th]:px-4 [&_th]:py-2.5 [&_th]:text-left [&_th]:font-semibold [&_th]:whitespace-nowrap [&_tbody_tr]:border-b [&_tbody_tr]:border-borde [&_td]:px-4 [&_td]:py-2.5 [&_td]:align-top">
      <EncabezadoDePantalla
        titulo={`Factura ${factura.numeroComprobante}`}
        accionPrincipal={
          <>
            {puedeGestionar && admiteCorreccion && (
              <button type="button" onClick={() => navegar(`/facturas/${factura.id}/editar`)}>
                Corregir datos
              </button>
            )}

            {puedeGestionar && admiteCobroYAnulacion && (
              <button type="button" onClick={() => setCobrando(true)}>
                Registrar cobro
              </button>
            )}

            {/* Permiso propio: quien gestiona sin `facturacion.anular` no ve este botón (FR-067). */}
            {puedeAnular && admiteCobroYAnulacion && (
              <button type="button" onClick={() => setAnulando(true)}>
                Anular
              </button>
            )}

            <button type="button" onClick={() => navegar('/facturas')}>
              Volver al listado
            </button>
          </>
        }
      />

      {/* Resultado que aparece sin que la pantalla cambie: se anuncia (convención [003]). */}
      {aviso !== null && <p role="status">{aviso}</p>}
      {error !== null && <p role="alert">{error}</p>}

      {factura.estado === 'anulada' && (
        <p role="status">
          Esta factura está anulada. No se corrige, no se cobra y no vuelve a estado pendiente.
        </p>
      )}

      <section aria-labelledby="titulo-comprobante-ficha">
        <h2 id="titulo-comprobante-ficha">Comprobante</h2>

        <dl>
          <dt>Tipo de comprobante</dt>
          <dd>{NOMBRES_DE_TIPO_COMPROBANTE[factura.tipoComprobante]}</dd>

          <dt>Tipo de facturación</dt>
          <dd>{NOMBRES_DE_TIPO_FACTURACION[factura.tipoFacturacion]}</dd>

          <dt>Condición de venta</dt>
          <dd>{NOMBRES_DE_CONDICION_DE_VENTA[factura.condicionDeVenta]}</dd>

          <dt>Período</dt>
          <dd>
            {String(factura.mes).padStart(2, '0')}/{factura.anio}
          </dd>

          <dt>Fecha de facturación</dt>
          <dd>{formatearFecha(factura.fecha)}</dd>

          <dt>Detalle</dt>
          <dd>{factura.detalle ?? '—'}</dd>

          <dt>Estado</dt>
          <dd>{NOMBRES_DE_ESTADO[factura.estado]}</dd>

          {factura.fechaCobro !== null && (
            <>
              <dt>Fecha de cobro</dt>
              <dd>{formatearFecha(factura.fechaCobro)}</dd>
            </>
          )}

          {factura.motivoAnulacion !== null && (
            <>
              <dt>Motivo de la anulación</dt>
              <dd>{factura.motivoAnulacion}</dd>
            </>
          )}
        </dl>
      </section>

      {/* El aviso va arriba de los dos bloques congelados, permanente (FR-034, FR-034a). */}
      <p role="note">{AVISO_DATOS_CONGELADOS}</p>

      <section aria-labelledby="titulo-emisor">
        <h2 id="titulo-emisor">Emisor</h2>

        <dl>
          <dt>Razón social</dt>
          <dd>{factura.emisor.razonSocial}</dd>

          <dt>CUIT</dt>
          <dd>{factura.emisor.cuit}</dd>

          <dt>Domicilio</dt>
          <dd>{factura.emisor.domicilio}</dd>

          <dt>Condición de IVA</dt>
          <dd>{factura.emisor.condicionIva}</dd>

          <dt>Ingresos brutos</dt>
          <dd>{factura.emisor.ingresosBrutos ?? '—'}</dd>

          <dt>Inicio de actividades</dt>
          <dd>
            {factura.emisor.inicioActividades === null
              ? '—'
              : formatearFecha(factura.emisor.inicioActividades)}
          </dd>

          <dt>Punto de venta</dt>
          <dd>{factura.emisor.puntoDeVenta ?? '—'}</dd>

          <dt>CBU</dt>
          <dd>{factura.emisor.cbu ?? '—'}</dd>

          <dt>Teléfono</dt>
          <dd>{factura.emisor.telefono ?? '—'}</dd>

          <dt>Email</dt>
          <dd>{factura.emisor.email ?? '—'}</dd>
        </dl>
      </section>

      <section aria-labelledby="titulo-cliente-ficha">
        <h2 id="titulo-cliente-ficha">Cliente</h2>

        <dl>
          <dt>Razón social</dt>
          {/* La congelada, con la palabra `Inactivo` si dejó el padrón después (FR-011, US3 esc. 9). */}
          <dd>{nombreDeCliente(factura.cliente)}</dd>

          <dt>CUIT</dt>
          <dd>{factura.cliente.cuit}</dd>

          <dt>Domicilio</dt>
          <dd>{factura.cliente.domicilio}</dd>
        </dl>
      </section>

      <section aria-labelledby="titulo-viajes-ficha">
        <h2 id="titulo-viajes-ficha">Viajes incluidos</h2>

        {/* Una factura anulada devolvió sus viajes a `rendido`, así que ya no los incluye. Se dice con
            palabras en vez de mostrar una tabla vacía, que se leería como un error de carga
            (data-model §Anular). */}
        {factura.viajes.length === 0 && (
          <p role="status">
            {factura.estado === 'anulada'
              ? 'Al anularse, sus viajes volvieron a estado rendido y quedaron disponibles para ' +
                'facturar de nuevo. El detalle de los que tenía quedó impreso en el documento.'
              : 'Esta factura no tiene viajes asociados.'}
          </p>
        )}

        <table>
          <caption>Viajes de la factura {factura.numeroComprobante}</caption>
          <thead>
            <tr>
              <th scope="col">Número</th>
              <th scope="col">Fecha</th>
              <th scope="col">Remito</th>
              <th scope="col">Origen</th>
              <th scope="col">Destino</th>
              <th scope="col" className="text-right">Importe</th>
            </tr>
          </thead>
          <tbody>
            {factura.viajes.map((viaje) => (
              <tr key={viaje.id}>
                <td>
                  <Link to={`/viajes/${viaje.id}`}>{viaje.numero}</Link>
                </td>
                <td>{formatearFecha(viaje.fecha)}</td>
                <td>{viaje.numeroRemito ?? '—'}</td>
                <td>{viaje.origen}</td>
                <td>{viaje.destino}</td>
                <td className="text-right font-medium">{formatearPesos(viaje.importe)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>

      <section aria-labelledby="titulo-importes-ficha">
        <h2 id="titulo-importes-ficha">Importes</h2>

        <dl>
          <dt>Neto</dt>
          <dd>{formatearPesos(factura.neto)}</dd>

          <dt>IVA ({factura.alicuota}%)</dt>
          <dd>{formatearPesos(factura.iva)}</dd>

          <dt>Total</dt>
          <dd>{formatearPesos(factura.total)}</dd>

          <dt>CAE</dt>
          <dd>{factura.cae}</dd>

          <dt>Vencimiento del CAE</dt>
          <dd>{formatearFecha(factura.caeVencimiento)}</dd>

          <dt>Vencimiento de pago</dt>
          <dd>{formatearFecha(factura.vencimientoPago)}</dd>
        </dl>
      </section>

      {/* Las dos direcciones de la referencia de refacturación (FR-050). */}
      {factura.reemplazaA !== null && (
        <p>
          Reemplaza a la factura{' '}
          <Link to={`/facturas/${factura.reemplazaA.id}`}>
            {factura.reemplazaA.numeroComprobante}
          </Link>{' '}
          del {formatearFecha(factura.reemplazaA.fecha)}, anulada.
        </p>
      )}

      {factura.reemplazadaPor !== null && (
        <p>
          Reemplazada por la Refacturación{' '}
          <Link to={`/facturas/${factura.reemplazadaPor.id}`}>
            {factura.reemplazadaPor.numeroComprobante}
          </Link>{' '}
          del {formatearFecha(factura.reemplazadaPor.fecha)}.
        </p>
      )}

      <section aria-labelledby="titulo-documento">
        <h2 id="titulo-documento">Documento</h2>

        {/* Se abre **en línea**, sin bajarlo y abrirlo a mano (FR-031a, convención [003]). */}
        <a href={factura.documentoUrl} target="_blank" rel="noreferrer">
          Ver el documento
        </a>

        <p>{NOTA_DEL_DOCUMENTO}</p>
      </section>

      <section aria-labelledby="titulo-historial-factura">
        <h2 id="titulo-historial-factura">Historial</h2>

        <table>
          <caption>Historial de la factura {factura.numeroComprobante}</caption>
          <thead>
            <tr>
              <th scope="col">Estado anterior</th>
              <th scope="col">Estado nuevo</th>
              <th scope="col">Usuario</th>
              <th scope="col">Fecha y hora</th>
            </tr>
          </thead>
          <tbody>
            {factura.historial.map((entrada, indice) => (
              <tr key={`${entrada.ocurridoEn}-${indice}`}>
                {/* Una entrada sin estado nuevo es una **corrección de datos**: la ausencia es la
                    marca, y el sistema no registra qué campos cambiaron (FR-037). */}
                {entrada.estadoNuevo === null ? (
                  <>
                    <td colSpan={2}>
                      <em>Corrección de datos</em>
                    </td>
                  </>
                ) : (
                  <>
                    <td>{entrada.estadoAnterior === null ? '—' : nombreDelEstado(entrada.estadoAnterior)}</td>
                    <td>{nombreDelEstado(entrada.estadoNuevo)}</td>
                  </>
                )}
                <td>{entrada.usuario}</td>
                <td>{formatearInstante(entrada.ocurridoEn)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>

      {cobrando && (
        <RegistrarCobro
          numero={factura.numeroComprobante}
          fechaPropuesta={hoyEnIso()}
          trabajando={trabajando}
          onRegistrar={cobrar}
          onCancelar={() => setCobrando(false)}
        />
      )}

      {anulando && (
        <ConfirmacionAnulacion
          numero={factura.numeroComprobante}
          cantidadDeViajes={factura.viajes.length}
          trabajando={trabajando}
          onConfirmar={anular}
          onCancelar={() => setAnulando(false)}
        />
      )}
    </section>
  )
}

/**
 * El nombre del estado guardado que sale en el historial.
 *
 * Son tres —`pendiente`, `pagada`, `anulada`— y no cuatro: `vencida` es derivado y nunca aparece en el
 * historial, porque nadie la escribió (FR-041).
 */
function nombreDelEstado(estado: string): string {
  return NOMBRES_DE_ESTADO[estado as keyof typeof NOMBRES_DE_ESTADO] ?? estado
}

/** Hoy en `yyyy-MM-dd`, construido con los tres números para no pasar por `new Date(iso)`. */
function hoyEnIso(): string {
  const hoy = new Date()

  return `${hoy.getFullYear()}-${String(hoy.getMonth() + 1).padStart(2, '0')}-${String(
    hoy.getDate(),
  ).padStart(2, '0')}`
}
