import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { Aviso } from '../../../compartido/ui/Aviso'
import { AsideDeFicha, BloqueDeAside, CifraDestacada } from '../../../compartido/ui/AsideDeFicha'
import { TablaDesplazable } from '../../../compartido/ui/Listado'
import { Boton } from '../../../compartido/ui/Boton'
import { Callout } from '../../../compartido/ui/Callout'
import { Estado } from '../../../compartido/ui/Estado'
import { FichaCuerpo, FichaSeccion } from '../../../compartido/ui/Ficha'
import { EntradaDeLineaDeTiempo, LineaDeTiempo } from '../../../compartido/ui/LineaDeTiempo'
import { TokenDeIdentificador } from '../../../compartido/ui/TokenDeIdentificador'
import { IconoAnulado, IconoEnRegla } from '../../../compartido/ui/iconos'
import { useCallback, useEffect, useState } from 'react'
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { formatearFecha, formatearInstante, hoyEnIso } from '../../../compartido/fechas'
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
        {error !== null ? (
          <Aviso tono="error" rol="alert">{error}</Aviso>
        ) : (
          <p role="status" className="m-0 text-[13px] text-ink-soft">Cargando…</p>
        )}
      </section>
    )
  }

  // Las acciones que cada estado admite (contracts/README §Acciones).
  const admiteCobroYAnulacion = factura.estado === 'pendiente' || factura.estado === 'vencida'
  const admiteCorreccion = admiteCobroYAnulacion || factura.estado === 'pagada'

  return (
    <section>
      <EncabezadoDePantalla
        titulo="Factura"
        volverA={{ ruta: '/facturas', etiqueta: 'Volver al listado' }}
        resumen={
          <span className="flex flex-wrap items-center gap-x-2 gap-y-1">
            <TokenDeIdentificador numero={factura.numeroComprobante} />
            <span>{nombreDeCliente(factura.cliente)}</span>
            <span aria-hidden="true">
              ·
            </span>
            <span>{formatearFecha(factura.fecha)}</span>
            <span aria-hidden="true">
              ·
            </span>
            {/* El estado sube al encabezado y sale de la lista de datos (FR-052). */}
            <Estado
              valor={factura.estado}
              texto={NOMBRES_DE_ESTADO[factura.estado]}
              forma="pastilla"
            />
          </span>
        }
        accionPrincipal={
          puedeGestionar || puedeAnular ? (
            <>
              {/*
                **La acción principal es *Registrar cobro***, que es lo que la ficha viene a hacer
                (FR-019). *Corregir datos* la acompaña como secundaria y *Anular* es destructiva:
                hasta ahora las tres se dibujaban idénticas porque el encabezado las estilaba por
                el tipo del botón. **Ningún verbo se reescribe** (FR-066).
              */}
              {puedeGestionar && admiteCorreccion && (
                <Boton
                  variante="secundario"
                  onClick={() => navegar(`/facturas/${factura.id}/editar`)}
                >
                  Corregir datos
                </Boton>
              )}

              {/* Permiso propio: quien gestiona sin `facturacion.anular` no ve este botón (FR-067). */}
              {puedeAnular && admiteCobroYAnulacion && (
                <Boton
                  variante="destructivo"
                  onClick={() => setAnulando(true)}
                  icono={<IconoAnulado className="size-3" />}
                >
                  Anular
                </Boton>
              )}

              {puedeGestionar && admiteCobroYAnulacion && (
                <Boton
                  variante="primario"
                  onClick={() => setCobrando(true)}
                  icono={<IconoEnRegla className="size-3" />}
                >
                  Registrar cobro
                </Boton>
              )}
            </>
          ) : undefined
        }
      />

      {/*
        Resultado que aparece sin que la pantalla cambie: se anuncia, y el rol va en el elemento que
        **contiene** el texto (convención [003]).
      */}
      {aviso !== null && (
        <p
          role="status"
          className="mb-[18px] rounded-card border border-line bg-estado-rendido-bg px-[18px] py-4 text-[13px] leading-5 font-medium text-estado-rendido"
        >
          {aviso}
        </p>
      )}
      {error !== null && (
        <p
          role="alert"
          className="mb-[18px] rounded-card border border-line bg-danger-bg px-[18px] py-4 text-[13px] leading-5 font-medium text-danger-text"
        >
          {error}
        </p>
      )}

      {/*
        Una factura anulada se queda **sin acción principal**, y el callout dice qué pasa y por qué
        no hay salida (FR-058). Sin él, la ficha se lee como una a la que le faltan botones.
      */}
      {factura.estado === 'anulada' && (
        <div className="mb-[18px]">
          <Callout tono="anulado" titulo="Factura anulada">
            <span role="status">
              Esta factura está anulada. No se corrige, no se cobra y no vuelve a estado pendiente.
              Al anularse, sus viajes volvieron a estado rendido: para volver a cobrarlos, emitile
              una refacturación al cliente desde Nueva factura.
            </span>
          </Callout>
        </div>
      )}

      <FichaCuerpo
        aside={
          <AsideDeFicha
            /* El dato de más valor de una factura es su importe total (data-model §8). */
            destacado={
              <div className="flex flex-col gap-3.5">
                <CifraDestacada rotulo="Total" valor={formatearPesos(factura.total)} />
                <dl className="m-0 grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-[12.5px]">
                  <dt className="text-faint">Neto</dt>
                  <dd className="m-0 text-right font-medium">{formatearPesos(factura.neto)}</dd>
                  <dt className="text-faint">IVA ({factura.alicuota}%)</dt>
                  <dd className="m-0 text-right font-medium">{formatearPesos(factura.iva)}</dd>
                </dl>
              </div>
            }
          >
            <BloqueDeAside titulo="Cobro">
              <p className="m-0">
                Vence el {formatearFecha(factura.vencimientoPago)}
              </p>
              {factura.fechaCobro !== null ? (
                <p className="m-0 text-[12.5px] text-faint">
                  Cobrada el {formatearFecha(factura.fechaCobro)}
                </p>
              ) : (
                /* El vacío se escribe: dice qué falta, nunca un guión (FR-038). */
                <p className="atenuada m-0 text-[12.5px]">Sin cobro registrado</p>
              )}
            </BloqueDeAside>

            <BloqueDeAside titulo="Cliente">
              {/* La congelada, con la palabra `Inactivo` si dejó el padrón después (FR-011 del
                  Módulo 6). */}
              <p className="m-0 font-semibold">{nombreDeCliente(factura.cliente)}</p>
              <p className="m-0 font-mono text-[12.5px] text-faint">{factura.cliente.cuit}</p>
              <p className="m-0 text-[12.5px] text-faint">{factura.cliente.domicilio}</p>
            </BloqueDeAside>

            <BloqueDeAside titulo="Documento">
              {/* Se abre **en línea**, sin bajarlo y abrirlo a mano (convención [003]). */}
              <a
                href={factura.documentoUrl}
                target="_blank"
                rel="noreferrer"
                className="text-brand underline underline-offset-2"
              >
                Ver el documento
              </a>
              <p className="m-0 text-[11.5px] text-faint">{NOTA_DEL_DOCUMENTO}</p>
            </BloqueDeAside>
          </AsideDeFicha>
        }
      >
        <FichaSeccion titulo="Comprobante" id="titulo-comprobante-ficha">
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
            <dd>{factura.detalle ?? <span className="atenuada">Sin detalle</span>}</dd>

            <dt>CAE</dt>
            <dd className="font-mono">{factura.cae}</dd>

            <dt>Vencimiento del CAE</dt>
            <dd>{formatearFecha(factura.caeVencimiento)}</dd>

            {factura.motivoAnulacion !== null && (
              <>
                <dt>Motivo de la anulación</dt>
                <dd>{factura.motivoAnulacion}</dd>
              </>
            )}
          </dl>
        </FichaSeccion>

        <FichaSeccion titulo="Viajes incluidos" id="titulo-viajes-ficha">
          {/* Una factura anulada devolvió sus viajes a `rendido`, así que ya no los incluye. Se dice
              con palabras en vez de mostrar una tabla vacía, que se leería como un error de carga. */}
          {factura.viajes.length === 0 && (
            <p role="status" className="m-0 px-[22px] py-5 text-[13px] leading-5 text-ink-soft">
              {factura.estado === 'anulada'
                ? 'Al anularse, sus viajes volvieron a estado rendido y quedaron disponibles para ' +
                  'facturar de nuevo. El detalle de los que tenía quedó impreso en el documento.'
                : 'Esta factura no tiene viajes asociados.'}
            </p>
          )}

          {factura.viajes.length > 0 && (
            <TablaDesplazable>
              <table className="w-full border-collapse text-[13px]">
                <caption className="sr-only">
                  Viajes de la factura {factura.numeroComprobante}
                </caption>
                <thead className="bg-surface-soft">
                  <tr className="[&>th]:border-b [&>th]:border-line [&>th]:px-5 [&>th]:py-2.5 [&>th]:text-left [&>th]:text-[9.5px] [&>th]:font-bold [&>th]:tracking-[0.14em] [&>th]:text-encabezado [&>th]:uppercase">
                    <th scope="col">Número</th>
                    <th scope="col">Fecha</th>
                    <th scope="col">Remito</th>
                    {/* `Origen` + `Destino` nombran un solo concepto: la ruta (FR-047). */}
                    <th scope="col">Ruta</th>
                    <th scope="col" className="text-right">
                      Importe
                    </th>
                  </tr>
                </thead>
                <tbody className="[&>tr]:border-b [&>tr]:border-line [&>tr:last-child]:border-b-0 [&_td]:px-5 [&_td]:py-[13px]">
                  {factura.viajes.map((viaje) => (
                    <tr key={viaje.id}>
                      <td>
                        <TokenDeIdentificador a={`/viajes/${viaje.id}`} numero={viaje.numero} />
                      </td>
                      <td>{formatearFecha(viaje.fecha)}</td>
                      <td className="font-mono">
                        {viaje.numeroRemito ?? (
                          <span className="atenuada font-sans">Sin remito</span>
                        )}
                      </td>
                      <td className="whitespace-nowrap">
                        {viaje.origen}{' '}
                        <span aria-hidden="true" className="text-dim">
                          →
                        </span>{' '}
                        {viaje.destino}
                      </td>
                      <td className="text-right font-bold whitespace-nowrap">
                        {formatearPesos(viaje.importe)}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </TablaDesplazable>
          )}
        </FichaSeccion>

        <FichaSeccion titulo="Emisor" id="titulo-emisor">
          {/* El aviso de datos congelados es permanente, no un tooltip (FR-034, FR-034a). */}
          <p role="note" className="m-0 border-b border-line px-[22px] py-3.5 text-[12.5px] text-faint">
            {AVISO_DATOS_CONGELADOS}
          </p>

          <dl>
            <dt>Razón social</dt>
            <dd>{factura.emisor.razonSocial}</dd>

            <dt>CUIT</dt>
            <dd className="font-mono">{factura.emisor.cuit}</dd>

            <dt>Domicilio</dt>
            <dd>{factura.emisor.domicilio}</dd>

            <dt>Condición de IVA</dt>
            <dd>{factura.emisor.condicionIva}</dd>

            <dt>Ingresos brutos</dt>
            <dd className="font-mono">
              {factura.emisor.ingresosBrutos ?? (
                <span className="atenuada font-sans">Sin número cargado</span>
              )}
            </dd>

            <dt>Inicio de actividades</dt>
            <dd>
              {factura.emisor.inicioActividades === null ? (
                <span className="atenuada">Sin fecha cargada</span>
              ) : (
                formatearFecha(factura.emisor.inicioActividades)
              )}
            </dd>

            <dt>Punto de venta</dt>
            <dd className="font-mono">
              {factura.emisor.puntoDeVenta ?? (
                <span className="atenuada font-sans">Sin punto de venta</span>
              )}
            </dd>

            <dt>CBU</dt>
            <dd className="font-mono">
              {factura.emisor.cbu ?? <span className="atenuada font-sans">Sin CBU cargado</span>}
            </dd>

            <dt>Teléfono</dt>
            <dd>{factura.emisor.telefono ?? <span className="atenuada">Sin teléfono</span>}</dd>

            <dt>Email</dt>
            <dd>{factura.emisor.email ?? <span className="atenuada">Sin email</span>}</dd>
          </dl>
        </FichaSeccion>

        {/* Las dos direcciones de la referencia de refacturación (FR-050 del Módulo 6). */}
        {(factura.reemplazaA !== null || factura.reemplazadaPor !== null) && (
          <FichaSeccion titulo="Refacturación" id="titulo-refacturacion">
            <div className="flex flex-col gap-2 px-[22px] py-5 text-[13px]">
              {factura.reemplazaA !== null && (
                <p className="m-0">
                  Reemplaza a la factura{' '}
                  <Link
                    to={`/facturas/${factura.reemplazaA.id}`}
                    className="text-brand underline underline-offset-2"
                  >
                    {factura.reemplazaA.numeroComprobante}
                  </Link>{' '}
                  del {formatearFecha(factura.reemplazaA.fecha)}, anulada.
                </p>
              )}

              {factura.reemplazadaPor !== null && (
                <p className="m-0">
                  Reemplazada por la Refacturación{' '}
                  <Link
                    to={`/facturas/${factura.reemplazadaPor.id}`}
                    className="text-brand underline underline-offset-2"
                  >
                    {factura.reemplazadaPor.numeroComprobante}
                  </Link>{' '}
                  del {formatearFecha(factura.reemplazadaPor.fecha)}.
                </p>
              )}
            </div>
          </FichaSeccion>
        )}

        <FichaSeccion titulo="Historial" id="titulo-historial-factura">
          {/* El historial deja de ser una tabla de cuatro columnas: la transición va completa en
              una línea y el paso actual va marcado (FR-054). */}
          <div className="px-[22px] py-5">
            <LineaDeTiempo>
              {factura.historial.map((entrada, indice) => (
                <EntradaDeLineaDeTiempo
                  key={`${entrada.ocurridoEn}-${indice}`}
                  actual={indice === factura.historial.length - 1}
                  cuando={formatearInstante(entrada.ocurridoEn)}
                  quien={entrada.usuario}
                  que={
                    /* Una entrada sin estado nuevo es una **corrección de datos**: la ausencia es
                       la marca, y el sistema no registra qué campos cambiaron (FR-037). */
                    entrada.estadoNuevo === null ? (
                      <em>Corrección de datos</em>
                    ) : entrada.estadoAnterior === null ? (
                      nombreDelEstado(entrada.estadoNuevo)
                    ) : (
                      <>
                        {nombreDelEstado(entrada.estadoAnterior)}{' '}
                        <span aria-hidden="true" className="text-dim">
                          →
                        </span>{' '}
                        {nombreDelEstado(entrada.estadoNuevo)}
                      </>
                    )
                  }
                />
              ))}
            </LineaDeTiempo>
          </div>
        </FichaSeccion>
      </FichaCuerpo>

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
